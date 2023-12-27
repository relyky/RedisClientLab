using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedisClientLab.Services
{
  internal class RandomService
  {
    readonly IConfiguration _config;

    public RandomService(IConfiguration config)
    {
      _config = config;
    }

    public string GetRandomGuid()
    {
      // 測試 services injection
      Console.WriteLine($"{_config["OutputFolder"]}");

      return Guid.NewGuid().ToString();
    }
  }
}
