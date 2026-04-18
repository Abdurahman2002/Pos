using System.Security.Claims;

namespace NewsApp2.Models.Entities
{
    public class StaticClaims
    {
        public static readonly IReadOnlyList<Claim> All = new List<Claim>
      {
          new Claim("Create", "true"),
          new Claim("Edit", "true"),
          new Claim("Delete", "true"),
          new Claim("EditUser", "true"),
          new Claim("SiteState", "true"),
                    new Claim("InventoryCreate", "true"),
                    new Claim("InventoryEdit", "true"),
                    new Claim("InventoryDelete", "true"),
                    new Claim("InventoryApprove", "true")

      };

    }
}
