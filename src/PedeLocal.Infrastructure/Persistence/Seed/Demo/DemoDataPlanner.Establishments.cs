using PedeLocal.Application.Common;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Features;
using PedeLocal.Domain.Pricing;
using PedeLocal.Domain.Subscriptions;

namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

public static partial class DemoDataPlanner
{
    /// <summary>One showcase (demo-best) establishment per city, matching the task's
    /// `merchant.<cidade>@pedelocal.dev` account — always the first establishment planned for that
    /// city, always Large size, always both Delivery+Pickup, always an ACTIVE subscription, with
    /// order generation later deliberately spreading today's orders across every status for it.</summary>
    private static readonly string[] ShowcaseCategoryPerCity = ["Supermercado", "Restaurante", "Pizzaria"];

    private enum SubscriptionTier { Active, Grace, Suspended, BlockedAdmin }

    private static void PlanEstablishments(Ctx ctx)
    {
        var typeOrder = ShuffledTypeCycle(ctx.Rng);
        var typeCursor = 0;

        for (var cityIndex = 0; cityIndex < ctx.Options.CityCount; cityIndex++)
        {
            var cityFx = DemoFixtureData.Cities[cityIndex];
            var establishmentCount = ctx.Options.EstablishmentsPerCity[cityIndex];
            var noSubscriptionLocalIndex = establishmentCount >= 2 ? 1 : -1;

            for (var localIndex = 0; localIndex < establishmentCount; localIndex++)
            {
                var isShowcase = localIndex == 0;
                var typeFixture = isShowcase
                    ? DemoFixtureData.EstablishmentTypes.Single(t => t.CategoryName == ShowcaseCategoryPerCity[cityIndex])
                    : DemoFixtureData.EstablishmentTypes[typeOrder[typeCursor++ % typeOrder.Length]];

                var size = isShowcase
                    ? DemoFixtureData.EstablishmentSize.Large
                    : WeightedPick(ctx.Rng,
                        (DemoFixtureData.EstablishmentSize.Small, 45),
                        (DemoFixtureData.EstablishmentSize.Medium, 35),
                        (DemoFixtureData.EstablishmentSize.Large, 20));

                var globalIndex = ctx.NextCounter();
                var name = GenerateEstablishmentName(ctx.Rng, typeFixture, globalIndex, out var slug);

                var establishmentId = DemoIdFactory.For(ctx.Profile, "establishment", globalIndex);

                var (street, number, district, lat, lng) = RandomAddressParts(ctx, cityFx, globalIndex);
                var address = new Address
                {
                    Id = DemoIdFactory.For(ctx.Profile, "establishment-address", globalIndex),
                    CityId = DemoPlaceholders.City(ctx.Profile, cityIndex),
                    Type = AddressType.Urban, // EstablishmentService.CreateAsync always forces Urban for establishment addresses — mirrored here.
                    Street = street,
                    Number = number,
                    District = district,
                    ZipCode = $"173{(70000 + globalIndex) % 100000:D5}",
                    Latitude = lat,
                    Longitude = lng
                };
                ctx.Addresses.Add(address);

                var operationalStatus = isShowcase
                    ? OperationalStatus.Open
                    : WeightedPick(ctx.Rng,
                        (OperationalStatus.Open, 70),
                        (OperationalStatus.ClosedManual, 10),
                        (OperationalStatus.ClosedSchedule, 10),
                        (OperationalStatus.Paused, 10));

                var establishment = new Establishment
                {
                    Id = establishmentId,
                    CityId = DemoPlaceholders.City(ctx.Profile, cityIndex),
                    Name = name,
                    Slug = slug,
                    Document = $"{10000000000000L + globalIndex:D14}",
                    Phone = $"149{globalIndex % 100000000:D8}",
                    Email = $"contato@{slug}.dev",
                    CategoryId = DemoPlaceholders.Category(ctx.Profile, typeFixture.CategoryName),
                    AddressId = address.Id,
                    OperationalStatus = operationalStatus,
                    CommercialStatus = CommercialStatus.Active // overwritten below once the subscription tier is known.
                };
                if (operationalStatus == OperationalStatus.Paused)
                {
                    ApplyPause(ctx, establishment);
                }
                ctx.Establishments.Add(establishment);

                ctx.EstablishmentSettingsList.Add(new EstablishmentSettings
                {
                    EstablishmentId = establishment.Id,
                    IsVisible = ctx.Rng.Next(100) < 95,
                    AcceptsNewOrders = ctx.Rng.Next(100) < 90
                });

                var supportsDelivery = isShowcase || ctx.Rng.Next(100) < 85;
                DeliveryPricingSettings? pricing = null;
                if (supportsDelivery)
                {
                    pricing = PlanDeliveryPricing(ctx, establishment.Id);
                    ctx.DeliveryPricingSettingsList.Add(pricing);
                }

                PlanEstablishmentFeatures(ctx, establishment, typeFixture);

                // Owner (ESTABLISHMENT_ADMIN) — the showcase gets the predictable merchant.<cidade>@pedelocal.dev
                // account from the task; every other establishment gets a filler dono.<slug>@demo.pedelocal.dev
                // account. Real EstablishmentService.CreateAsync always creates exactly one such owner per
                // establishment — mirrored here.
                var ownerEmail = isShowcase ? $"merchant.{cityFx.EmailSlug}@pedelocal.dev" : $"dono.{slug}@demo.pedelocal.dev";
                // The showcase owner's id is profile-INDEPENDENT (see DemoIdFactory.ForShared) —
                // its email is the same literal merchant.<cidade>@pedelocal.dev regardless of which
                // profile is active, so its id must be too, or seeding a second profile on top of a
                // first without resetting would try to insert a second row with that same email.
                var owner = new Domain.Identity.User
                {
                    Id = isShowcase
                        ? DemoIdFactory.ForShared("establishment-owner", cityFx.EmailSlug)
                        : DemoIdFactory.For(ctx.Profile, "establishment-owner", globalIndex),
                    Name = $"{PickOne(ctx.Rng, DemoFixtureData.FirstNames)} {PickOne(ctx.Rng, DemoFixtureData.LastNames)}",
                    Email = ownerEmail,
                    PhoneNumber = $"148{globalIndex % 100000000:D8}",
                    PasswordHash = string.Empty, // filled once by DemoDataSeeder (shared hash) before insert.
                    IsActive = true
                };
                ctx.Users.Add(owner);
                ctx.UserRoles.Add(new Domain.Identity.UserRole
                {
                    UserId = owner.Id,
                    RoleId = DemoPlaceholders.Role(ctx.Profile, Domain.Identity.RoleNames.EstablishmentAdmin)
                });
                ctx.EstablishmentUsers.Add(new EstablishmentUser
                {
                    EstablishmentId = establishment.Id,
                    UserId = owner.Id,
                    StaffRole = Domain.Identity.RoleNames.EstablishmentAdmin
                });
                if (isShowcase)
                {
                    // Only the showcase (merchant.<cidade>@pedelocal.dev) owner goes in the printed
                    // credentials table — every other establishment's dono.<slug>@demo.pedelocal.dev
                    // account is just as real/functional, but listing ~60 of them would defeat the
                    // point of a short, memorable table. See docs/DEMO_DATA.md.
                    ctx.Credentials.Add(new DemoCredentialRow("ESTABLISHMENT_ADMIN", cityFx.Name, ownerEmail, name));
                }

                var tier = isShowcase
                    ? SubscriptionTier.Active
                    : WeightedPick(ctx.Rng,
                        (SubscriptionTier.Active, 73),
                        (SubscriptionTier.Grace, 15),
                        (SubscriptionTier.Suspended, 8),
                        (SubscriptionTier.BlockedAdmin, 4));

                var hasNoSubscription = !isShowcase && localIndex == noSubscriptionLocalIndex &&
                    ctx.EstablishmentsWithoutSubscription.Count < 3;

                if (hasNoSubscription)
                {
                    ctx.EstablishmentsWithoutSubscription.Add(establishment.Id);
                    establishment.CommercialStatus = CommercialStatus.Active;
                }
                else
                {
                    PlanSubscription(ctx, establishment, size, tier);
                }

                var catalog = PlanCatalog(ctx, establishment, typeFixture, size, isShowcase, globalIndex);

                ctx.EstablishmentInfos.Add(new EstablishmentPlanInfo
                {
                    Establishment = establishment,
                    CityIndex = cityIndex,
                    Size = size,
                    SupportsDelivery = supportsDelivery,
                    Address = address,
                    Pricing = pricing,
                    IsShowcase = isShowcase,
                    Catalog = catalog
                });
            }
        }
    }

    private static int[] ShuffledTypeCycle(Random rng)
    {
        var indices = Enumerable.Range(0, DemoFixtureData.EstablishmentTypes.Length).ToArray();
        for (var i = indices.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        return indices;
    }

    /// <summary>The display <see cref="Establishment.Name"/> stays clean/human ("Mercadinho da
    /// Vila"), but <paramref name="slug"/> ALWAYS gets `-{globalIndex}` appended — <c>globalIndex</c>
    /// already carries the profile-offset (see <c>Ctx.GlobalCounter</c>), which is what guarantees
    /// the slug (and everything derived from it, like a filler establishment owner's
    /// dono.&lt;slug&gt;@demo.pedelocal.dev email) can never collide, neither within one profile's
    /// run nor across two different profiles seeded on top of each other without a reset in
    /// between. A name-only collision (two establishments sharing the same prefix+suffix combo,
    /// which the modest fixture pool makes non-negligible once you have ~60+ establishments) is
    /// still just a cosmetic duplicate display name, never a broken unique constraint.</summary>
    private static string GenerateEstablishmentName(Random rng, DemoFixtureData.EstablishmentTypeFixture typeFixture, int globalIndex, out string slug)
    {
        var prefix = PickOne(rng, typeFixture.NamePrefixes);
        var suffix = PickOne(rng, typeFixture.NameSuffixes);
        var name = $"{prefix} {suffix}";
        slug = $"{SlugGenerator.Slugify(name)}-{globalIndex}";
        return name;
    }

    private static (string Street, string Number, string District, decimal Lat, decimal Lng) RandomAddressParts(
        Ctx ctx, DemoFixtureData.CityFixture cityFx, int uniqueSeed)
    {
        var street = PickOne(ctx.Rng, DemoFixtureData.StreetNames);
        var number = (100 + uniqueSeed % 2000).ToString();
        var district = PickOne(ctx.Rng, DemoFixtureData.Districts);
        // Jitter within roughly ±0.025 degrees (~2.5km) of the city center — stays comfortably
        // inside every establishment's DeliveryPricingSettings.DeliveryRadiusKm (5-20km below).
        var latJitter = (decimal)(ctx.Rng.NextDouble() - 0.5) * 0.05m;
        var lngJitter = (decimal)(ctx.Rng.NextDouble() - 0.5) * 0.05m;
        return (street, number, district, cityFx.BaseLatitude + latJitter, cityFx.BaseLongitude + lngJitter);
    }

    /// <summary>Every establishment gets its own flat fee and radius — varied per the demo spec so
    /// admin/merchant screens show a realistic spread rather than one identical value everywhere.</summary>
    private static DeliveryPricingSettings PlanDeliveryPricing(Ctx ctx, Guid establishmentId) => new()
    {
        EstablishmentId = establishmentId,
        DeliveryFee = RandomDecimal(ctx.Rng, 5, 15),
        DeliveryRadiusKm = RandomDecimal(ctx.Rng, 5, 20, 0)
    };

    /// <summary>Varies pause reason and expiry across the small number of Paused establishments —
    /// one indefinite pause, one with a future expiry (still blocking orders), one already past its
    /// expiry (exercises EstablishmentPauseExpiryJob's auto-resume / the lazy IsOperationallyOpen
    /// check at order-creation time).</summary>
    private static void ApplyPause(Ctx ctx, Establishment establishment)
    {
        establishment.PauseReason = PickOne(ctx.Rng,
        [
            EstablishmentPauseReason.HighDemand, EstablishmentPauseReason.NoDriverAvailable,
            EstablishmentPauseReason.TemporaryClosure, EstablishmentPauseReason.Other
        ]);
        establishment.PausedAtUtc = ctx.NowUtc.AddMinutes(-ctx.Rng.Next(10, 240));
        establishment.PausedByUserId = DemoPlaceholders.SuperAdmin(ctx.Profile);
        establishment.PausedUntilUtc = WeightedPick(ctx.Rng,
            ((DateTime?)ctx.NowUtc.AddMinutes(ctx.Rng.Next(15, 180)), 40), // still paused
            ((DateTime?)ctx.NowUtc.AddMinutes(-ctx.Rng.Next(5, 60)), 30),  // expired, auto-resume candidate
            ((DateTime?)null, 30));                                       // indefinite, manual resume only
    }

    private static readonly HashSet<string> StockHeavyCategories = ["Açougue", "Farmácia", "Adega", "Hortifruti"];
    private static readonly HashSet<string> WholesaleCandidateCategories = ["Mercado", "Supermercado"];

    /// <summary>Sprinkles a few real <see cref="FeatureKeys"/> flags — only ever the real keys the
    /// domain already defines, never invented ones. Only `Enabled=true` rows are created (no row at
    /// all means "never configured", same as every other establishment).</summary>
    private static void PlanEstablishmentFeatures(Ctx ctx, Establishment establishment, DemoFixtureData.EstablishmentTypeFixture typeFixture)
    {
        void MaybeAdd(string key, int chancePercent)
        {
            if (ctx.Rng.Next(100) < chancePercent)
            {
                ctx.EstablishmentFeatures.Add(new EstablishmentFeature
                {
                    Id = DemoIdFactory.For(ctx.Profile, "feature", $"{establishment.Id}:{key}"),
                    EstablishmentId = establishment.Id,
                    FeatureKey = key,
                    Enabled = true
                });
            }
        }

        if (StockHeavyCategories.Contains(typeFixture.CategoryName))
        {
            MaybeAdd(FeatureKeys.AdvancedStock, 35);
        }
        if (WholesaleCandidateCategories.Contains(typeFixture.CategoryName))
        {
            MaybeAdd(FeatureKeys.WholesaleMode, 20);
        }
        MaybeAdd(FeatureKeys.CustomReport, 10);
        MaybeAdd(FeatureKeys.SpecialOrderFlow, 5);
    }

    private static void PlanSubscription(Ctx ctx, Establishment establishment, DemoFixtureData.EstablishmentSize size, SubscriptionTier tier)
    {
        var (min, max) = size switch
        {
            DemoFixtureData.EstablishmentSize.Small => (59.90m, 79.90m),
            DemoFixtureData.EstablishmentSize.Medium => (99.90m, 149.90m),
            _ => (199.90m, 299.90m)
        };
        var monthlyAmount = RandomDecimal(ctx.Rng, min, max);

        DateOnly dueDate;
        SubscriptionPaymentStatus paymentStatus;
        switch (tier)
        {
            case SubscriptionTier.Grace:
                dueDate = ctx.Today.AddDays(-ctx.Rng.Next(1, 5)); // 1-4 days overdue, inside the 5-day grace window.
                paymentStatus = SubscriptionPaymentStatus.OverdueGrace;
                establishment.CommercialStatus = CommercialStatus.OverdueGrace;
                break;
            case SubscriptionTier.Suspended:
                dueDate = ctx.Today.AddDays(-ctx.Rng.Next(6, 31)); // past the 5-day grace window.
                paymentStatus = SubscriptionPaymentStatus.SuspendedPayment;
                establishment.CommercialStatus = CommercialStatus.SuspendedPayment;
                break;
            case SubscriptionTier.BlockedAdmin:
                dueDate = ctx.Today.AddDays(ctx.Rng.Next(5, 26)); // payment itself is fine — the block is administrative.
                paymentStatus = SubscriptionPaymentStatus.Active;
                establishment.CommercialStatus = CommercialStatus.SuspendedAdmin;
                break;
            default:
                dueDate = ctx.Today.AddDays(ctx.Rng.Next(5, 26));
                paymentStatus = SubscriptionPaymentStatus.Active;
                establishment.CommercialStatus = CommercialStatus.Active;
                break;
        }

        var subscription = new EstablishmentSubscription
        {
            Id = DemoIdFactory.For(ctx.Profile, "subscription", establishment.Id.ToString()),
            EstablishmentId = establishment.Id,
            MonthlyAmount = monthlyAmount,
            BillingDay = Math.Clamp(dueDate.Day, 1, 28),
            DueDate = dueDate,
            GraceDays = 5,
            PaymentStatus = paymentStatus
        };
        ctx.Subscriptions.Add(subscription);

        if (tier is SubscriptionTier.Active or SubscriptionTier.Grace or SubscriptionTier.BlockedAdmin)
        {
            var paymentCount = ctx.Rng.Next(1, 4);
            for (var i = 1; i <= paymentCount; i++)
            {
                var paidAt = ToUtc(ctx, dueDate.AddMonths(-i), 10, 0);
                ctx.Payments.Add(new Domain.Subscriptions.EstablishmentPayment
                {
                    Id = DemoIdFactory.For(ctx.Profile, "payment", $"{establishment.Id}:{i}"),
                    EstablishmentId = establishment.Id,
                    SubscriptionId = subscription.Id,
                    Amount = monthlyAmount,
                    PaymentMethod = PickOne(ctx.Rng, ["Pix", "Boleto", "Cartão de Crédito"]),
                    PaidAtUtc = paidAt,
                    RegisteredByUserId = DemoPlaceholders.SuperAdmin(ctx.Profile),
                    Notes = null
                });
            }
        }
    }

    private static readonly string[] ProductVariantSuffixes = ["", " Tradicional", " Especial", " Família", " Grande", " Pequeno"];
    private static readonly HashSet<string> HamburgueriaOptionCategories = ["Hambúrgueres"];
    private static readonly HashSet<string> PizzariaOptionCategories = ["Pizzas Tradicionais", "Pizzas Especiais"];

    private static List<CatalogProductInfo> PlanCatalog(
        Ctx ctx, Establishment establishment, DemoFixtureData.EstablishmentTypeFixture typeFixture,
        DemoFixtureData.EstablishmentSize size, bool isShowcase, int establishmentGlobalIndex)
    {
        var desiredCount = size switch
        {
            DemoFixtureData.EstablishmentSize.Small => ctx.Rng.Next(4, 7),
            DemoFixtureData.EstablishmentSize.Medium => ctx.Rng.Next(9, 14),
            _ => isShowcase ? ctx.Rng.Next(20, 26) : ctx.Rng.Next(17, 25)
        };

        var templates = typeFixture.Catalog;
        var menuCategoryIdByName = new Dictionary<string, Guid>();
        var usedNames = new HashSet<string>();
        var catalog = new List<CatalogProductInfo>();

        for (var productIndex = 0; productIndex < desiredCount; productIndex++)
        {
            var template = templates[productIndex % templates.Length];
            var variantIndex = productIndex / templates.Length;
            var name = template.Name + ProductVariantSuffixes[variantIndex % ProductVariantSuffixes.Length];
            if (!usedNames.Add(name))
            {
                name = $"{name} ({productIndex + 1})";
                usedNames.Add(name);
            }

            if (!menuCategoryIdByName.TryGetValue(template.MenuCategory, out var menuCategoryId))
            {
                menuCategoryId = DemoIdFactory.For(ctx.Profile, "menu-category", $"{establishment.Id}:{template.MenuCategory}");
                menuCategoryIdByName[template.MenuCategory] = menuCategoryId;
                ctx.MenuCategories.Add(new MenuCategory
                {
                    Id = menuCategoryId,
                    EstablishmentId = establishment.Id,
                    Name = template.MenuCategory,
                    SortOrder = menuCategoryIdByName.Count
                });
            }

            var price = RandomDecimal(ctx.Rng, template.MinPrice, template.MaxPrice);
            var isControlled = template.ControlledStockCandidate && ctx.Rng.Next(100) < 55;
            int? stockQuantity = null;
            if (isControlled)
            {
                stockQuantity = WeightedPick(ctx.Rng,
                    (ctx.Rng.Next(20, 91), 70),
                    (ctx.Rng.Next(2, 9), 20),
                    (0, 10));
            }

            var isAvailable = stockQuantity == 0
                ? ctx.Rng.Next(100) < 40
                : ctx.Rng.Next(100) < 92;

            var product = new Product
            {
                Id = DemoIdFactory.For(ctx.Profile, "product", $"{establishmentGlobalIndex}:{productIndex}"),
                EstablishmentId = establishment.Id,
                MenuCategoryId = menuCategoryId,
                Name = name,
                Description = template.Description,
                Price = price,
                StockMode = isControlled ? StockMode.Controlled : StockMode.Uncontrolled,
                StockQuantity = stockQuantity,
                IsAvailable = isAvailable,
                Version = 0
            };
            ctx.Products.Add(product);

            if (HamburgueriaOptionCategories.Contains(template.MenuCategory))
            {
                AttachOptionGroup(ctx, product, "Adicionais",
                    ("Bacon Extra", RandomDecimal(ctx.Rng, 4, 7)),
                    ("Queijo Cheddar Extra", RandomDecimal(ctx.Rng, 3, 5)));
            }
            else if (PizzariaOptionCategories.Contains(template.MenuCategory))
            {
                AttachOptionGroup(ctx, product, "Borda Recheada",
                    ("Borda Catupiry", RandomDecimal(ctx.Rng, 8, 12)),
                    ("Borda Cheddar", RandomDecimal(ctx.Rng, 8, 12)));
            }

            catalog.Add(new CatalogProductInfo { Product = product, IsControlled = isControlled, RemainingStock = stockQuantity });
        }

        return catalog;
    }

    private static void AttachOptionGroup(Ctx ctx, Product product, string groupName, params (string Name, decimal PriceDelta)[] options)
    {
        var group = new ProductOptionGroup
        {
            Id = DemoIdFactory.For(ctx.Profile, "option-group", $"{product.Id}:{groupName}"),
            ProductId = product.Id,
            Name = groupName,
            Required = false,
            Min = 0,
            Max = options.Length
        };
        ctx.ProductOptionGroups.Add(group);

        foreach (var (optionName, priceDelta) in options)
        {
            ctx.ProductOptions.Add(new ProductOption
            {
                Id = DemoIdFactory.For(ctx.Profile, "option", $"{group.Id}:{optionName}"),
                ProductOptionGroupId = group.Id,
                Name = optionName,
                PriceDelta = priceDelta,
                IsAvailable = true
            });
        }
    }
}
