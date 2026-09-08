using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

public static partial class DemoDataPlanner
{
    private static void PlanCustomers(Ctx ctx)
    {
        for (var cityIndex = 0; cityIndex < ctx.Options.CityCount; cityIndex++)
        {
            var cityFx = DemoFixtureData.Cities[cityIndex];
            var list = new List<CustomerInfo>();
            ctx.CustomersByCity[cityIndex] = list;

            list.Add(CreateCustomer(ctx, cityIndex, cityFx, $"customer.{cityFx.EmailSlug}@pedelocal.dev", "CUSTOMER"));

            for (var i = 0; i < ctx.Options.FillerCustomersPerCity; i++)
            {
                var globalIndex = ctx.NextCounter();
                var email = $"cliente{globalIndex:D3}@demo.pedelocal.dev";
                list.Add(CreateCustomer(ctx, cityIndex, cityFx, email, credentialRole: null));
            }
        }
    }

    private static CustomerInfo CreateCustomer(Ctx ctx, int cityIndex, DemoFixtureData.CityFixture cityFx, string email, string? credentialRole)
    {
        var uniqueSeed = ctx.NextCounter();
        var name = $"{PickOne(ctx.Rng, DemoFixtureData.FirstNames)} {PickOne(ctx.Rng, DemoFixtureData.LastNames)}";
        // The predictable customer.<cidade>@pedelocal.dev account's id is profile-INDEPENDENT (see
        // DemoIdFactory.ForShared) — same reasoning as the showcase establishment owner above.
        var user = new User
        {
            Id = credentialRole is not null ? DemoIdFactory.ForShared("customer", email) : DemoIdFactory.For(ctx.Profile, "customer", email),
            Name = name,
            Email = email,
            PhoneNumber = $"147{uniqueSeed % 100000000:D8}",
            PasswordHash = string.Empty,
            IsActive = true
        };
        ctx.Users.Add(user);
        ctx.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = DemoPlaceholders.Role(ctx.Profile, RoleNames.Customer) });

        var (street, number, district, lat, lng) = RandomAddressParts(ctx, cityFx, uniqueSeed);
        var address = new Address
        {
            Id = DemoIdFactory.For(ctx.Profile, "customer-address", email),
            CityId = DemoPlaceholders.City(ctx.Profile, cityIndex),
            UserId = user.Id,
            Type = AddressType.Urban,
            Label = "Casa",
            Street = street,
            Number = number,
            District = district,
            ZipCode = $"173{(71000 + uniqueSeed) % 100000:D5}",
            Latitude = lat,
            Longitude = lng
        };
        ctx.Addresses.Add(address);

        if (credentialRole is not null)
        {
            ctx.Credentials.Add(new DemoCredentialRow(credentialRole, cityFx.Name, email, null));
        }

        return new CustomerInfo { User = user, Address = address };
    }

    private static void PlanDrivers(Ctx ctx)
    {
        for (var cityIndex = 0; cityIndex < ctx.Options.CityCount; cityIndex++)
        {
            var cityFx = DemoFixtureData.Cities[cityIndex];
            var list = new List<DriverInfo>();
            ctx.DriversByCity[cityIndex] = list;

            list.Add(CreateDriver(ctx, cityIndex, $"driver.{cityFx.EmailSlug}@pedelocal.dev", "DRIVER", cityFx));

            for (var i = 0; i < ctx.Options.FillerDriversPerCity; i++)
            {
                var globalIndex = ctx.NextCounter();
                var email = $"entregador{globalIndex:D3}@demo.pedelocal.dev";
                list.Add(CreateDriver(ctx, cityIndex, email, credentialRole: null, cityFx));
            }
        }
    }

    private static DriverInfo CreateDriver(Ctx ctx, int cityIndex, string email, string? credentialRole, DemoFixtureData.CityFixture cityFx)
    {
        var uniqueSeed = ctx.NextCounter();
        var name = $"{PickOne(ctx.Rng, DemoFixtureData.FirstNames)} {PickOne(ctx.Rng, DemoFixtureData.LastNames)}";
        // The predictable driver.<cidade>@pedelocal.dev account's id is profile-INDEPENDENT (see
        // DemoIdFactory.ForShared) — same reasoning as the showcase establishment owner above.
        var user = new User
        {
            Id = credentialRole is not null ? DemoIdFactory.ForShared("driver-user", email) : DemoIdFactory.For(ctx.Profile, "driver-user", email),
            Name = name,
            Email = email,
            PhoneNumber = $"146{uniqueSeed % 100000000:D8}",
            PasswordHash = string.Empty,
            IsActive = true
        };
        ctx.Users.Add(user);
        ctx.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = DemoPlaceholders.Role(ctx.Profile, RoleNames.Driver) });

        var cpf = $"{20000000000L + uniqueSeed:D11}";
        var vehicleType = WeightedPick(ctx.Rng, (VehicleType.Motorcycle, 60), (VehicleType.Bike, 25), (VehicleType.Car, 15));
        var pixKeyType = PickOne(ctx.Rng, new[] { PixKeyType.Cpf, PixKeyType.Email, PixKeyType.Phone, PixKeyType.Random });
        var pixKey = pixKeyType switch
        {
            PixKeyType.Cpf => cpf,
            PixKeyType.Email => email,
            PixKeyType.Phone => user.PhoneNumber,
            _ => $"demo-pix-{uniqueSeed:D6}"
        };

        // The predictable driver.<cidade>@pedelocal.dev account is always approved and immediately
        // usable — it's meant for logging in and testing the driver app right away.
        var isPredictable = credentialRole is not null;
        var approvalStatus = isPredictable || ctx.Rng.Next(100) < 90
            ? DriverApprovalStatus.Approved
            : DriverApprovalStatus.PendingApproval;

        // Driver.UserId has a real unique DB index — since the predictable driver's User.Id is now
        // shared across profiles (above), its Driver row's id must be shared too, or seeding a
        // second profile without resetting first would try to insert a second Driver row for the
        // same (already unique-constrained) UserId.
        var driver = new Driver
        {
            Id = isPredictable ? DemoIdFactory.ForShared("driver", email) : DemoIdFactory.For(ctx.Profile, "driver", email),
            UserId = user.Id,
            CityId = DemoPlaceholders.City(ctx.Profile, cityIndex),
            Cpf = cpf,
            VehicleType = vehicleType,
            PixKeyType = pixKeyType,
            PixKey = pixKey,
            ApprovalStatus = approvalStatus,
            // Baseline Offline/Available split; PlanOrders may flip a couple of these to Busy when
            // it deliberately leaves them with a live in-progress delivery ("entrega atual").
            Status = approvalStatus == DriverApprovalStatus.Approved
                ? WeightedPick(ctx.Rng, (DriverStatus.Available, 50), (DriverStatus.Offline, 50))
                : DriverStatus.Offline,
            ApprovedAtUtc = approvalStatus == DriverApprovalStatus.Approved ? ctx.NowUtc.AddDays(-ctx.Rng.Next(5, 120)) : null
        };
        ctx.Drivers.Add(driver);

        if (credentialRole is not null)
        {
            ctx.Credentials.Add(new DemoCredentialRow(credentialRole, cityFx.Name, email, null));
        }

        return new DriverInfo { Driver = driver, Busy = false };
    }
}
