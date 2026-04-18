using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NewsApp2.Models.Entities;

namespace NewsApp2.Models
{
    public static class ModelBuilderExtensions
    {
        public static void Seed(this ModelBuilder modelBuilder)

        {
            modelBuilder.Entity<SiteInfo>().HasData(
                  new SiteInfo()
                  {
                      Id = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
                      Name = "Worldwide",
                      Activity = "News site",
                      About = "We are a specialized news website covering political, sports, and economic events, along with various other sections of general interest to readers. We always strive to provide distinguished and reliable content that reflects the ongoing developments on both the local and global stages.",
                      Created = new DateTime(2025, 9, 30)
                  });
            modelBuilder.Entity<Contact>().HasData(
              new Contact()
              {
                  Id = Guid.Parse("b27d3d62-8d55-4c2e-8b9c-34f6a6f2f1e2"),
                  Email = "W.Wide@Gmail.com",
                  Phone = "00218951234567",
                  Facebook = "Worldwide Facebook",
                  Twitter = "Worldwide Twitter",
                  Instagram = "Worldwide Instagram",
                  Created = new DateTime(2025, 9, 30)

              });
            
            modelBuilder.Entity<SiteState>().HasData(
             new SiteState()
             {
                 Id = Guid.Parse("6fa459ea-ee8a-3ca4-894e-db77e160355e"),
                 State = true,
                 ClosingMessage = "The site is temporarily closed for development",
                 Created = new DateTime(2025, 9, 30)

             });

            //--------------------Inventory Categories seed-----------------------------
            //modelBuilder.Entity<Category>().HasData(
            //    new Category
            //    {
            //        Id = Guid.Parse("1a8af6e8-6016-4d81-9d3e-3b7e0e73f8b1"),
            //        Code = "CAT-RAW",
            //        Name = "Raw Materials",
            //        IsActive = true,
            //        Created = Convert.ToDateTime("1/1/2026")
            //    },
            //    new Category
            //    {
            //        Id = Guid.Parse("2a9fbe41-1b63-46dc-8f29-975f2bb87c5d"),
            //        Code = "CAT-FIN",
            //        Name = "Finished Goods",
            //        IsActive = true,
            //        Created = Convert.ToDateTime("1/1/2026")
            //    }
            //);


            //--------------------Roles-----------------------------

            var progRoleId = "2cdc7bbd-449b-4fa5-87c7-4d8cf0683bea";
            var programmerId = "a1b495e6-dcb6-4763-9994-a4d74b93105c";
            var adminRoleId = "1b8e70e8-1dc6-4ff9-bd93-5a11d5c11a77";
            var adminUserId = "7e5d8740-fcb7-47c8-bdad-6f22072fe37f";
            var concurrencyStampRole = "a1404a92-7520-4989-8c46-5922377cb2d0";
            var concurrencyStampUser = "f67b0714-171b-4b5f-a459-817a40f3041d";
            var SecurityStamp = "1d4afc4a-ef8a-4e5e-a09a-7ee026455e41";
            var adminRoleConcurrency = "58ab0c6c-10e6-4e30-a28c-f2f43e8de879";
            var adminUserConcurrency = "ce470a13-3765-46f4-989f-afb5e3341fe9";
            var adminSecurityStamp = "f3eeac15-69d9-4f95-a5e5-9c30d4f08157";

            var salesManagerRoleId = "24a53d0e-b1f6-4c57-94d7-0d5b7f2b6122";
            var salesOfficerRoleId = "d2f8be95-9b13-4e8c-bc95-6f57a08b56f1";
            var cashierRoleId = "6c99e53a-1163-4504-9b68-15e9b9ec48ea";
            var salesManagerConcurrency = "5a6a1d5d-0b70-4a83-85fb-4ddf7b2b9c64";
            var salesOfficerConcurrency = "9e07e5dd-fd8b-4a1a-8e45-8c7b2d3dfe7c";
            var cashierConcurrency = "5d0655d1-6b89-4557-b9a7-7f07666a5568";


            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = progRoleId,
                    Name = "Prog",
                    NormalizedName = "Prog".ToUpper(),
                    ConcurrencyStamp = concurrencyStampRole
                },
                new IdentityRole
                {
                    Id = adminRoleId,
                    Name = "Admin",
                    NormalizedName = "Admin".ToUpper(),
                    ConcurrencyStamp = adminRoleConcurrency
                }
            );

            modelBuilder.Entity<IdentityRole>().HasData(
                new IdentityRole
                {
                    Id = salesManagerRoleId,
                    Name = "SalesManager",
                    NormalizedName = "SalesManager".ToUpper(),
                    ConcurrencyStamp = salesManagerConcurrency
                },
                new IdentityRole
                {
                    Id = salesOfficerRoleId,
                    Name = "SalesOfficer",
                    NormalizedName = "SalesOfficer".ToUpper(),
                    ConcurrencyStamp = salesOfficerConcurrency
                },
                new IdentityRole
                {
                    Id = cashierRoleId,
                    Name = "Cashier",
                    NormalizedName = "Cashier".ToUpper(),
                    ConcurrencyStamp = cashierConcurrency
                }
            );

            //-------------------Programmer-----------------------------

            string programmerName = "Programmer@Gmail.com";
            //string programmerName = "Programmer@ly.com";

            var Programmer = new ApplicationUser
            {
                Id = programmerId,
                UserName = programmerName,
                NormalizedUserName = programmerName.ToUpper(),
                Email = programmerName,
                NormalizedEmail = programmerName.ToUpper(),
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                LockoutEnabled = true,
                SecurityStamp = SecurityStamp,
                ConcurrencyStamp = concurrencyStampUser,
                //CreatedDate = DateTime.UtcNow,
                CreatedDate = new DateTime(2025, 1, 1),
                //هذا الهاش الخاص بالباسوورد 111
                PasswordHash = "AQAAAAIAAYagAAAAENggG9+6Z01XNeB9YmF/XmQybN3d/MpCrkCkJ58k03l2udJQx0IaIujsHHxHRpulzQ=="
            };

            modelBuilder.Entity<ApplicationUser>().HasData(Programmer);

            var adminEmail = "admin@newsapp2.local";
            var admin = new ApplicationUser
            {
                Id = adminUserId,
                UserName = adminEmail,
                NormalizedUserName = adminEmail.ToUpper(),
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpper(),
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                LockoutEnabled = true,
                SecurityStamp = adminSecurityStamp,
                ConcurrencyStamp = adminUserConcurrency,
                CreatedDate = new DateTime(2026, 1, 1),
                Approval = true,
                PasswordHash = "AQAAAAIAAYagAAAAENggG9+6Z01XNeB9YmF/XmQybN3d/MpCrkCkJ58k03l2udJQx0IaIujsHHxHRpulzQ=="
            };

            modelBuilder.Entity<ApplicationUser>().HasData(admin);

            modelBuilder.Entity<InventorySettings>().HasData(
                new InventorySettings
                {
                    Id = Guid.Parse("d8ec402f-4f11-4f5e-97ed-c9dc2a58a232"),
                    IsSingleWarehouseMode = true,
                    BaseCurrencyCode = "EUR",
                    DinarCurrencyCode = "LYD",
                    SingleWarehouseName = "Main Warehouse",
                    DefaultRateSource = "Manual",
                    MaxCashierDiscountPercent = 10m,
                    Created = new DateTime(2026, 1, 1)
                }
            );

            modelBuilder.Entity<Customer>().HasData(
                new Customer
                {
                    Id = Guid.Parse("7e2efb6c-0cb2-430f-92af-6e0ad720f105"),
                    Name = "مبيعات يومية",
                    Note = "عميل افتراضي لمبيعات الكاش اليومية",
                    Created = new DateTime(2026, 1, 1)
                }
            );


            //-------------Add User to Role--------------------
            modelBuilder.Entity<IdentityUserRole<string>>().HasData(
                new IdentityUserRole<string>
                {
                    RoleId = progRoleId,
                    UserId = programmerId
                },
                new IdentityUserRole<string>
                {
                    RoleId = adminRoleId,
                    UserId = adminUserId
                });
            //-----------------------------------------------------





        }

    }

}
