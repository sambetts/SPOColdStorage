using Microsoft.AspNetCore.Mvc;
using SPO.ColdStorage.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace SPO.ColdStorage.Tests
{
    [Route("api/[controller]")]
    public class ValuesController : Controller
    {
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        [HttpGet("/_api/v2.0/drives/{driveId}/items/{graphItemId}/analytics/allTime")]
        public ItemAnalyticsRepsonse GetAnalytics(string driveId, string graphItemId)
        {
            return new ItemAnalyticsRepsonse { };
        }
    }
}
