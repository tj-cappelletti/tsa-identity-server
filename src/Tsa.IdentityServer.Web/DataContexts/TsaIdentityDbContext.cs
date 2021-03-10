using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Tsa.IdentityServer.Web.DataContexts
{
    public class TsaIdentityDbContext : IdentityDbContext
    {
        public TsaIdentityDbContext(DbContextOptions<TsaIdentityDbContext> options) : base(options) { }
    }
}
