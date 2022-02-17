using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPO.ColdStorage.Entities;
using SPO.ColdStorage.Entities.Configuration;
using SPO.ColdStorage.Migration.Engine;
using SPO.ColdStorage.Models;
using SPO.ColdStorage.Web.Models;

namespace SPO.ColdStorage.Web.Controllers
{
    /// <summary>
    /// Handles React app requests for app configuration
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    [Route("[controller]")]
    public class AppConfigurationController : ControllerBase
    {
        private readonly ILogger<AppConfigurationController> _logger;
        private readonly SPOColdStorageDbContext _context;
        private readonly Config _config;

        public AppConfigurationController(ILogger<AppConfigurationController> logger, SPOColdStorageDbContext context, Config config)
        {
            _logger = logger;
            this._context = context;
            this._config = config;
        }


        // Generate app ServiceConfiguration + storage configuration + key to read blobs
        // GET: AppConfiguration/ServiceConfiguration
        [HttpGet("[action]")]
        public ActionResult<ServiceConfiguration> GetServiceConfiguration()
        {
            var client = new BlobServiceClient(_config.ConnectionStrings.Storage);

            // Generate a new shared-access-signature
            var sasUri = client.GenerateAccountSasUri(AccountSasPermissions.List | AccountSasPermissions.Read,
                DateTime.Now.AddDays(1),
                AccountSasResourceTypes.Container | AccountSasResourceTypes.Object);

            // Return for react app
            return new ServiceConfiguration 
            {
                StorageInfo = new StorageInfo
                {
                    AccountURI = client.Uri.ToString(),
                    SharedAccessToken = sasUri.Query,
                    ContainerName = _config.BlobContainerName
                },
                SearchConfiguration = new SearchConfiguration 
                {
                    IndexName = _config.SearchConfig.IndexName,
                    QueryKey = _config.SearchConfig.QueryKey, 
                    ServiceName = _config.SearchConfig.ServiceName
                }
            };
        }

        // GET: AppConfiguration/GetSharePointToken
        [HttpGet("[action]")]
        public async Task<string> GetSharePointToken()
        {
            var app = await AuthUtils.GetNewClientApp(_config);

            var result = await app.AuthForSharePointOnline(_config.BaseServerAddress);
            return result.AccessToken;
        }

        // GET: AppConfiguration/GetGetMigrationTargets
        [HttpGet("[action]")]
        public async Task<ActionResult<IEnumerable<SiteListFilterConfig>>> GetMigrationTargets()
        {
            var targets = await _context.TargetSharePointSites.ToListAsync();
            var returnList = new List<SiteListFilterConfig>();
            foreach (var target in targets)
                returnList.Add(target.ToSiteListFilterConfig());

            return returnList;
        }

        /// <summary>
        /// Set migration config
        /// </summary>
        /// <param name="targets">List of sites + site config</param>
        /// <returns></returns>
        // POST: AppConfiguration/SetMigrationTargets
        [HttpPost("[action]")]
        public async Task<ActionResult> SetMigrationTargets(List<SiteListFilterConfig> targets)
        {
            if (targets == null || targets.Count == 0)
            {
                return BadRequest($"{nameof(targets)} is null");
            }
            foreach (var target in targets)
            {
                if (!target.IsValid)
                {
                    return BadRequest("Invalid config data");
                }
            }
            // Verify auth works with 1st item
            try
            {
                await AuthUtils.GetClientContext(_config, targets[0].RootURL);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating authentication to SharePoint Online");
                return BadRequest($"Got '{ex.Message}' trying to get a token for SPO authentication. Check service configuration.");
            }


            // Remove old target configuration & set new
            var oldTargetSites = await _context.TargetSharePointSites.ToListAsync();
            _context.TargetSharePointSites.RemoveRange(oldTargetSites);

            // Verify each site exists
            foreach (var target in targets)
            {
                try
                {
                    var siteContext = await AuthUtils.GetClientContext(_config, target.RootURL);
                    siteContext.Load(siteContext.Web);
                    await siteContext.ExecuteQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error validating site");
                    return BadRequest($"Got '{ex.Message}' validating SPO site URL '{target}'. It's not a valid SharePoint site-collection URL?");
                }

                var dbObj = new Entities.DBEntities.TargetMigrationSite(target);

                // Assuming no error, save to SQL
                _context.TargetSharePointSites.Add(dbObj);
            }
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}