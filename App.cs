using Microsoft.Extensions.Configuration;
using RedisClientLab.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NRedisStack;
using NRedisStack.RedisStackCommands;
using StackExchange.Redis;
using System.Runtime.Serialization;

namespace RedisClientLab
{
  internal class App
  {
    readonly IConfiguration _config;
    readonly RandomService _randSvc;

    public App(IConfiguration config, RandomService randSvc)
    {
      _config = config;
      _randSvc = randSvc;
    }

    /// <summary>
    /// 取代原本 Program.Main() 函式的效用。
    /// </summary>
    public void Run(string[] args)
    {
      ////## for Redis-Stack-Server，有支援帳密連線。
      //using var redis = ConnectionMultiplexer.Connect(new ConfigurationOptions
      //{
      //  EndPoints = { { "localhost", 6379 } },
      //  User = "default",  // use your Redis user. More info https://redis.io/docs/management/security/acl/
      //  Password = "mypassword", // use your Redis password
      //  Ssl = false,
      //  SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
      //  AllowAdmin = true, // 啟動管理員模式
      //});

      //## for Redis，不需帳密就可連線。
      using var redis = ConnectionMultiplexer.Connect("localhost:6379", options => {
        options.AllowAdmin = true; // 啟動管理員模式。才可抓取 keyspace_hits, keyspace_misses 狀態值。
      });

      // 可同時有多個 Redis DB 存在，故需選取一個。
      IDatabase db = redis.GetDatabase();

      // String 結構 ---------------------------------------
      db.StringSet("foo", "我出運了", TimeSpan.FromSeconds(3));
      //db.StringSet("baz", 9876543210.123456789m); // 不支援 deciaml

      for (int i = 1; i <= 5; i++)
      {
        var value = db.StringGet("foo");
        Console.WriteLine(value.HasValue ? value : "nil");

        // 取值 也 取時效
        var fooExpires = db.StringGetWithExpiry("foo");
        Console.WriteLine($"{fooExpires.Value} | {fooExpires.Expiry}");

        // 取值 並 重設時效
        var fooExpires2 = db.StringGetSetExpiry("foo", TimeSpan.FromSeconds(3));
        Console.WriteLine(fooExpires2.HasValue ? fooExpires2 : "nil");

        // 移除
        bool del_ret = db.KeyDelete("foo");
        Console.WriteLine(del_ret);

        System.Threading.SpinWait.SpinUntil(() => false, 1000);
      }

      // Hash 結構, 二層結構 ---------------------------------------
      var hash = new HashEntry[] {
        new HashEntry("name", "John"),
        new HashEntry("surname", "Smith"),
        new HashEntry("company", "Redis"),
        new HashEntry("age", "29"),
        new HashEntry("salary", "9876543210.123456789"),
      };

      db.HashSet("user-session:123", hash);

      // 取 Hash 全部
      var hashFields = db.HashGetAll("user-session:123");
      Console.WriteLine(String.Join("; ", hashFields));

      // 取 Hash  其中一個值
      var hashValue = db.HashGet("user-session:123", "age");
      Console.WriteLine(hashValue);

      // Set 結構 ---------------------------------------
      string mysetKey = "myset:123";
      //Console.WriteLine($"key: {mysetKey} := {db.KeyType(mysetKey)} | {db.KeyExists(mysetKey)}"); // 列出全部
      //db.KeyDelete(mysetKey);

      db.SetAdd(mysetKey, "我是字串", CommandFlags.FireAndForget);
      db.SetAdd(mysetKey, 166888, CommandFlags.FireAndForget);
      db.SetAdd(mysetKey, 889, CommandFlags.FireAndForget);

      Console.WriteLine(db.SetPop(mysetKey)); // pop 一筆
      Console.WriteLine(String.Join(", ", db.SetMembers(mysetKey))); // 列出全部

      Console.WriteLine("§§ List (Queue/Stack) 結構 ---------------------------------------");
      string mylistKey = "mylist:456";

      db.ListLeftPush(mylistKey, "abc");
      db.ListLeftPush(mylistKey, "走過路過別錯過。");


      Console.WriteLine("§§ SortedSet 結構 ---------------------------------------");
      string mysetKey2 = "myset:789";

      db.SortedSetAdd(mysetKey2, "我是字串", 4);
      db.SortedSetAdd(mysetKey2, 166888, 8);
      db.SortedSetAdd(mysetKey2, 889, 6);

      Console.WriteLine(db.SortedSetPop(mysetKey2)); // pop 一筆
      Console.WriteLine(db.SortedSetPop(mysetKey2)); // pop 一筆
      Console.WriteLine(db.SortedSetPop(mysetKey2)); // pop 一筆

      Console.WriteLine("§§ Hyper Log Log 結構 ---------------------------------------");
      // 估算大型數據集基數的場景，如：廣告點擊計數、唯一訪問者統計、按讚數等估算。

      db.HyperLogLogAdd("likeCnt", "4");
      db.HyperLogLogAdd("likeCnt", "5");
      db.HyperLogLogAdd("likeCnt", "3");

      Console.WriteLine($"likeCnt: {db.HyperLogLogLength("likeCnt")}"); // 按讚數

      Console.WriteLine("§§ Geo 結構 -----------------------------------------");
      db.GeoAdd("mygeokey", 111.11, 22.22, "某甲");
      db.GeoAdd("mygeokey", 131.11, 52.22, "某乙");

      var distance = db.GeoDistance("mygeokey", "某甲", "某乙");
      Console.WriteLine($"{"某甲"}與{"某乙"}的距離有{distance}公尺。");

      Console.WriteLine("§§ 計算 hit rate (需啟動 admin 模式) ----------------");

      //# 計算 hit rate  -----------------------------------------");
      var statsInfo = redis.GetServer("localhost:6379").Info("Stats")
                           .SelectMany(m => m.Select(c => c))
                           .Where(c => c.Key == "keyspace_hits" || c.Key == "keyspace_misses")
                           .ToDictionary(c => c.Key, c => c.Value);

      long hits = long.Parse(statsInfo["keyspace_hits"]);
      long misses = long.Parse(statsInfo["keyspace_misses"]);
      long total = hits + misses;

      double hitRate = (double)hits * 100.0d / (double)(total);
      Console.WriteLine($"Hit Rate → {hits} / {total} = {hitRate:#.00}%");

      //# Dump reids server all info;
      Console.WriteLine($"Dump reids server all info -----------------------------");

      var stssInfos = redis.GetServer("localhost:6379").Info();
      foreach (var info in stssInfos)
        foreach (var prop in info)
          Console.WriteLine($"Dump redis info: {info.Key}: {prop.Key} => {prop.Value}");

      redis.Dispose();
      Console.WriteLine("Press any key to continue.");
      Console.ReadKey();
    }
  }
}
