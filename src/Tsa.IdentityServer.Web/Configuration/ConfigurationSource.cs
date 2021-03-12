using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Tsa.IdentityServer.Web.Configuration
{
    public enum ConfigurationSource
    {
        [Description("No source is specified and system will not attempt to load configuration.")]
        None,
        
        [Description("The source is a JSON file that is stored with the project's source code. NOT FOR PRODUCTION!!")]
        Project,

        [Description("The source is a JSON file on the system hosting the applciation.")]
        SystemStorage,

        [Description("The source is a JSON file host in Azure Storage.")]
        AzureStorage
    }
}
