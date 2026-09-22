namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

/// <summary>Curated fictitious data pools used by <see cref="DemoDataPlanner"/> — no real brand
/// names, no real people. Everything here is generic/descriptive (e.g. "Cerveja Pilsen Lata
/// 350ml") on purpose: realistic enough to look like a real catalog, generic enough to never be
/// confused with an actual product/company. Picking is driven by the caller's seeded
/// <see cref="Random"/>, so the same profile always produces the same picks.</summary>
public static class DemoFixtureData
{
    /// <summary>`EmailSlug` is the exact local-part used by the predictable
    /// `customer.<slug>@pedelocal.dev` / `driver.<slug>@pedelocal.dev` / `merchant.<slug>@pedelocal.dev`
    /// accounts the task specifies verbatim — NOT derived from `SlugGenerator.Slugify(Name)` (that
    /// would produce "julio-mesquita" with a dash; the task's literal accounts use
    /// "juliomesquita").</summary>
    public sealed record CityFixture(string Name, string EmailSlug, decimal BaseLatitude, decimal BaseLongitude);

    /// <summary>The 3 launch cities — same names/order DevelopmentDataSeeder already uses, so the
    /// demo seeder reuses (never duplicates) those City rows when present. Coordinates are
    /// approximate town centers (São Paulo state interior) — plausible, not surveyed.</summary>
    public static readonly CityFixture[] Cities =
    [
        new("Guaimbê", "guaimbe", -21.8501m, -49.8988m),
        new("Júlio Mesquita", "juliomesquita", -21.9973m, -50.5859m),
        new("Getulina", "getulina", -21.7614m, -50.0511m)
    ];

    public enum EstablishmentSize { Small, Medium, Large }

    public sealed record EstablishmentTypeFixture(
        string CategoryName,
        string[] NamePrefixes,
        string[] NameSuffixes,
        ProductTemplate[] Catalog);

    public sealed record ProductTemplate(string MenuCategory, string Name, string Description, decimal MinPrice, decimal MaxPrice, bool ControlledStockCandidate);

    /// <summary>One entry per establishment type the task asked for. `NamePrefixes`/`NameSuffixes`
    /// combine (e.g. "Empório" + "do Vale") into fictitious trade names — never a real chain.</summary>
    public static readonly EstablishmentTypeFixture[] EstablishmentTypes =
    [
        new("Mercado", ["Mercadinho", "Mercado", "Empório", "Mercearia"], ["do Bairro", "Central", "da Vila", "Popular", "Bom Preço", "da Esquina"],
        [
            new("Grãos e Massas", "Arroz Branco Tipo 1 5kg", "Pacote de arroz branco tipo 1.", 18, 26, false),
            new("Grãos e Massas", "Feijão Carioca 1kg", "Pacote de feijão carioca.", 7, 11, false),
            new("Grãos e Massas", "Macarrão Espaguete 500g", "Massa seca tipo espaguete.", 4, 7, false),
            new("Bebidas", "Refrigerante Cola 2L", "Refrigerante sabor cola, garrafa 2 litros.", 8, 12, true),
            new("Bebidas", "Suco de Laranja 1L", "Suco de laranja integral.", 6, 10, false),
            new("Limpeza", "Detergente Neutro 500ml", "Detergente líquido para louças.", 2, 4, false),
            new("Limpeza", "Sabão em Pó 1kg", "Sabão em pó multiuso.", 12, 18, false),
            new("Higiene", "Papel Higiênico 12 rolos", "Papel higiênico folha dupla.", 15, 24, false),
            new("Hortifruti", "Banana Prata kg", "Banana prata fresca.", 4, 7, true),
            new("Hortifruti", "Tomate kg", "Tomate salada fresco.", 5, 9, true),
            new("Congelados", "Pizza Congelada Muçarela", "Pizza pronta congelada, sabor muçarela.", 14, 20, true)
        ]),
        new("Supermercado", ["Supermercado", "Hipermercado"], ["Economia", "Família", "Central", "Bom Lar", "Preço Bom", "Regional"],
        [
            new("Grãos e Massas", "Arroz Branco Tipo 1 5kg", "Pacote de arroz branco tipo 1.", 18, 26, false),
            new("Grãos e Massas", "Feijão Preto 1kg", "Pacote de feijão preto.", 7, 11, false),
            new("Açougue", "Picanha kg", "Corte bovino picanha.", 45, 70, true),
            new("Açougue", "Frango Inteiro kg", "Frango inteiro resfriado.", 9, 14, true),
            new("Padaria", "Pão Francês kg", "Pão francês fresco do dia.", 10, 16, false),
            new("Hortifruti", "Maçã Gala kg", "Maçã gala fresca.", 6, 9, true),
            new("Bebidas", "Água Mineral sem Gás 500ml", "Água mineral natural.", 2, 4, true),
            new("Bebidas", "Cerveja Pilsen Lata 350ml", "Cerveja pilsen, lata 350ml.", 3, 5, true),
            new("Limpeza", "Amaciante de Roupas 2L", "Amaciante concentrado.", 14, 20, false),
            new("Congelados", "Lasanha Congelada 600g", "Lasanha pronta congelada.", 16, 24, true),
            new("Higiene", "Sabonete em Barra Kit 4un", "Kit com 4 sabonetes.", 8, 13, false),
            new("Frios", "Queijo Muçarela Fatiado 200g", "Queijo muçarela fatiado.", 9, 14, true)
        ]),
        new("Adega", ["Adega", "Empório"], ["do Vinho", "Bem Servida", "da Vila", "Central", "do Sommelier", "Boa Safra"],
        [
            new("Cervejas", "Cerveja Pilsen Lata 350ml", "Cerveja pilsen, lata 350ml.", 3, 5, true),
            new("Cervejas", "Cerveja IPA Long Neck 355ml", "Cerveja estilo IPA, garrafa long neck.", 7, 12, true),
            new("Cervejas", "Cerveja Weiss 500ml", "Cerveja de trigo estilo weiss.", 8, 13, true),
            new("Refrigerantes", "Refrigerante Cola 2L", "Refrigerante sabor cola, 2 litros.", 8, 12, false),
            new("Refrigerantes", "Refrigerante Guaraná 2L", "Refrigerante sabor guaraná, 2 litros.", 8, 12, false),
            new("Água", "Água Mineral com Gás 500ml", "Água mineral gaseificada.", 3, 5, true),
            new("Energéticos", "Energético Tradicional 260ml", "Bebida energética lata 260ml.", 7, 11, true),
            new("Gelo", "Gelo em Cubos 2kg", "Saco de gelo em cubos.", 6, 10, true),
            new("Snacks", "Batata Chips Original 100g", "Salgadinho de batata.", 6, 9, false),
            new("Snacks", "Amendoim Torrado 200g", "Amendoim torrado e salgado.", 5, 8, false),
            new("Vinhos", "Vinho Tinto Seco 750ml", "Vinho tinto seco nacional.", 25, 45, true)
        ]),
        new("Hamburgueria", ["Burger", "Hamburgueria", "Point do Burger"], ["Artesanal", "do Chef", "da Vila", "Prime", "Smash", "Grill"],
        [
            new("Hambúrgueres", "X-Burger Clássico", "Pão, carne, queijo, alface e tomate.", 18, 26, false),
            new("Hambúrgueres", "X-Bacon Especial", "Pão, carne, queijo, bacon e molho especial.", 22, 32, false),
            new("Hambúrgueres", "Smash Duplo", "Dois hambúrgueres smash, queijo e cebola caramelizada.", 26, 38, false),
            new("Hambúrgueres", "Vegetariano Grelhado", "Hambúrguer de grão-de-bico grelhado.", 20, 28, false),
            new("Combos", "Combo Clássico + Fritas + Refri", "Hambúrguer clássico, batata frita e refrigerante.", 32, 45, false),
            new("Porções", "Batata Frita Média", "Porção de batata frita crocante.", 14, 20, false),
            new("Porções", "Onion Rings", "Porção de anéis de cebola empanados.", 16, 22, false),
            new("Adicionais", "Bacon Extra", "Porção adicional de bacon.", 4, 7, false),
            new("Adicionais", "Queijo Cheddar Extra", "Fatia extra de queijo cheddar.", 3, 5, false),
            new("Bebidas", "Refrigerante Lata 350ml", "Refrigerante em lata.", 5, 8, false),
            new("Bebidas", "Milk-shake Chocolate 400ml", "Milk-shake cremoso de chocolate.", 14, 20, false)
        ]),
        new("Pizzaria", ["Pizzaria", "Point da Pizza"], ["Bella Massa", "da Vila", "Tradição", "Forno a Lenha", "Sabor Real", "Napolitana"],
        [
            new("Pizzas Tradicionais", "Pizza Muçarela", "Molho de tomate, muçarela e orégano.", 32, 48, false),
            new("Pizzas Tradicionais", "Pizza Calabresa", "Molho de tomate, calabresa e cebola.", 34, 50, false),
            new("Pizzas Tradicionais", "Pizza Portuguesa", "Presunto, ovos, cebola, ervilha e muçarela.", 38, 55, false),
            new("Pizzas Especiais", "Pizza Quatro Queijos", "Muçarela, provolone, parmesão e gorgonzola.", 40, 58, false),
            new("Pizzas Especiais", "Pizza Frango com Catupiry", "Frango desfiado com catupiry.", 38, 54, false),
            new("Bordas", "Borda Recheada Catupiry", "Borda recheada com catupiry.", 8, 12, false),
            new("Bordas", "Borda Recheada Cheddar", "Borda recheada com cheddar.", 8, 12, false),
            new("Bebidas", "Refrigerante 2L", "Refrigerante garrafa 2 litros.", 9, 13, false),
            new("Sobremesas", "Pizza Doce Chocolate", "Pizza doce com chocolate ao leite.", 30, 42, false),
            new("Sobremesas", "Pudim de Leite Fatia", "Fatia de pudim de leite condensado.", 8, 12, false)
        ]),
        new("Restaurante", ["Restaurante", "Cantina", "Sabor"], ["Caseiro", "da Vila", "Bom Sabor", "Tempero de Casa", "Regional", "da Praça"],
        [
            new("Pratos Executivos", "Prato Executivo Frango Grelhado", "Frango grelhado, arroz, feijão e salada.", 22, 32, false),
            new("Pratos Executivos", "Prato Executivo Carne de Panela", "Carne de panela, arroz, feijão e purê.", 26, 36, false),
            new("Marmitas", "Marmita Fitness", "Peito de frango, arroz integral e legumes.", 20, 28, false),
            new("Marmitas", "Marmita Tradicional Grande", "Arroz, feijão, carne e acompanhamentos.", 24, 34, false),
            new("Saladas", "Salada Caesar com Frango", "Alface, frango grelhado, croutons e molho caesar.", 22, 30, false),
            new("Bebidas", "Suco Natural de Laranja 500ml", "Suco natural de laranja.", 8, 12, false),
            new("Bebidas", "Refrigerante Lata 350ml", "Refrigerante em lata.", 5, 8, false),
            new("Sobremesas", "Pudim de Leite", "Pudim de leite condensado.", 8, 12, false),
            new("Sobremesas", "Petit Gateau", "Bolo de chocolate com sorvete.", 14, 20, false)
        ]),
        new("Farmácia", ["Farmácia", "Drogaria"], ["Popular", "Saúde", "Central", "Bem Estar", "da Vila", "Vida"],
        [
            new("Analgésicos", "Analgésico Genérico 500mg 20cp", "Analgésico de venda livre, caixa com 20 comprimidos.", 6, 12, true),
            new("Antitérmicos", "Antitérmico Infantil Gotas 15ml", "Antitérmico pediátrico em gotas.", 12, 20, true),
            new("Vitaminas", "Vitamina C Efervescente 10cp", "Suplemento de vitamina C.", 10, 18, true),
            new("Higiene Pessoal", "Álcool em Gel 70% 500ml", "Álcool em gel antisséptico.", 8, 14, true),
            new("Higiene Pessoal", "Protetor Solar FPS 50 120ml", "Protetor solar corporal.", 35, 55, true),
            new("Primeiros Socorros", "Curativo Adesivo Kit 20un", "Kit de curativos adesivos variados.", 7, 12, false),
            new("Cuidados", "Repelente de Insetos 100ml", "Repelente corporal.", 15, 24, true),
            new("Cuidados", "Termômetro Digital", "Termômetro digital de uso doméstico.", 18, 30, true),
            new("Bebidas", "Água Mineral sem Gás 500ml", "Água mineral natural.", 2, 4, true)
        ]),
        new("Conveniência", ["Conveniência", "Mini Mercado", "Ponto"], ["24h", "Rápido", "da Esquina", "Express", "do Posto", "Popular"],
        [
            new("Bebidas", "Água Mineral sem Gás 500ml", "Água mineral natural.", 2, 4, true),
            new("Bebidas", "Refrigerante Lata 350ml", "Refrigerante em lata.", 4, 7, true),
            new("Bebidas", "Energético Tradicional 260ml", "Bebida energética lata 260ml.", 7, 11, true),
            new("Snacks", "Batata Chips Original 100g", "Salgadinho de batata.", 6, 9, false),
            new("Snacks", "Chocolate ao Leite 90g", "Barra de chocolate ao leite.", 6, 10, false),
            new("Sorvetes", "Picolé de Fruta", "Picolé sabor fruta natural.", 4, 7, true),
            new("Padaria", "Pão de Queijo Unidade", "Pão de queijo assado na hora.", 3, 5, false),
            new("Rápidos", "Sanduíche Natural", "Sanduíche natural de frango com salada.", 10, 16, false),
            new("Cigarros e Diversos", "Isqueiro Descartável", "Isqueiro de uso único.", 3, 5, false)
        ]),
        new("Açougue", ["Açougue", "Casa de Carnes"], ["do Bairro", "Central", "Boi Bom", "da Vila", "Tradição", "Premium"],
        [
            new("Carnes Bovinas", "Picanha kg", "Corte bovino picanha.", 45, 70, true),
            new("Carnes Bovinas", "Alcatra kg", "Corte bovino alcatra.", 32, 48, true),
            new("Carnes Bovinas", "Carne Moída kg", "Carne bovina moída na hora.", 24, 36, true),
            new("Carnes Suínas", "Costela Suína kg", "Costela suína resfriada.", 20, 30, true),
            new("Carnes Suínas", "Lombo Suíno kg", "Lombo suíno resfriado.", 22, 32, true),
            new("Aves", "Frango Inteiro kg", "Frango inteiro resfriado.", 9, 14, true),
            new("Aves", "Coxa e Sobrecoxa kg", "Coxa e sobrecoxa de frango.", 10, 15, true),
            new("Linguiças", "Linguiça Toscana kg", "Linguiça toscana resfriada.", 18, 26, true),
            new("Linguiças", "Linguiça Calabresa kg", "Linguiça calabresa defumada.", 20, 28, true),
            new("Acompanhamentos", "Carvão para Churrasco 5kg", "Saco de carvão vegetal.", 18, 26, false),
            new("Acompanhamentos", "Pão de Alho Congelado", "Pão de alho congelado, pronto para assar.", 10, 16, true)
        ]),
        new("Padaria", ["Padaria", "Panificadora"], ["Pão Quente", "Central", "da Vila", "Bom Pão", "Doce Sabor", "Estrela"],
        [
            new("Pães", "Pão Francês kg", "Pão francês fresco do dia.", 10, 16, false),
            new("Pães", "Pão de Forma Integral", "Pão de forma integral fatiado.", 8, 13, false),
            new("Pães", "Pão Doce Recheado", "Pão doce recheado com creme.", 5, 8, false),
            new("Salgados", "Coxinha de Frango", "Salgado frito recheado com frango.", 5, 8, false),
            new("Salgados", "Esfiha de Carne", "Esfiha aberta recheada com carne.", 4, 7, false),
            new("Doces", "Sonho Recheado", "Massa doce frita recheada com creme.", 5, 8, false),
            new("Doces", "Bolo de Fubá Fatia", "Fatia de bolo de fubá caseiro.", 6, 9, false),
            new("Bebidas", "Café Coado Copo 300ml", "Café coado na hora.", 4, 7, false),
            new("Bebidas", "Suco de Laranja 500ml", "Suco de laranja natural.", 6, 10, false),
            new("Frios", "Presunto Fatiado 200g", "Presunto cozido fatiado.", 8, 13, true),
            new("Frios", "Queijo Muçarela Fatiado 200g", "Queijo muçarela fatiado.", 9, 14, true)
        ]),
        new("Hortifruti", ["Hortifruti", "Sacolão", "Feira"], ["da Vila", "Fresco Bom", "Central", "Natureza", "Verde Vida", "Popular"],
        [
            new("Frutas", "Banana Prata kg", "Banana prata fresca.", 4, 7, true),
            new("Frutas", "Maçã Gala kg", "Maçã gala fresca.", 6, 9, true),
            new("Frutas", "Laranja Pera kg", "Laranja pera fresca.", 3, 6, true),
            new("Frutas", "Mamão Formosa kg", "Mamão formosa fresco.", 4, 7, true),
            new("Verduras", "Alface Crespa Unidade", "Alface crespa fresca.", 2, 4, true),
            new("Verduras", "Couve Manteiga Maço", "Maço de couve manteiga.", 3, 5, true),
            new("Legumes", "Tomate kg", "Tomate salada fresco.", 5, 9, true),
            new("Legumes", "Cenoura kg", "Cenoura fresca.", 4, 7, true),
            new("Legumes", "Batata kg", "Batata inglesa fresca.", 5, 8, true),
            new("Ovos", "Ovos Brancos Dúzia", "Dúzia de ovos brancos.", 8, 14, true),
            new("Temperos", "Cheiro-verde Maço", "Maço de cheiro-verde fresco.", 2, 4, true)
        ])
    ];

    public static readonly string[] VehicleColors = ["Prata", "Preto", "Branco", "Vermelho", "Azul", "Cinza"];

    public static readonly string[] FirstNames =
    [
        "Ana", "Bruno", "Carla", "Daniel", "Eduarda", "Fábio", "Gabriela", "Hugo", "Isabela", "João",
        "Karina", "Lucas", "Marina", "Nathan", "Olivia", "Pedro", "Queila", "Rafael", "Sabrina", "Thiago",
        "Ursula", "Vitor", "Wesley", "Yasmin", "Zeca", "Aline", "Breno", "Camila", "Diego", "Elaine",
        "Felipe", "Gustavo", "Helena", "Igor", "Juliana", "Kevin", "Larissa", "Mateus", "Natália", "Otávio"
    ];

    public static readonly string[] LastNames =
    [
        "Oliveira", "Souza", "Santos", "Pereira", "Almeida", "Ferreira", "Rodrigues", "Costa", "Martins", "Carvalho",
        "Gomes", "Ribeiro", "Barbosa", "Cardoso", "Nascimento", "Lopes", "Araújo", "Teixeira", "Moreira", "Correia"
    ];

    public static readonly string[] StreetNames =
    [
        "Rua das Flores", "Rua Sete de Setembro", "Avenida Brasil", "Rua XV de Novembro", "Rua São José",
        "Rua Rio Branco", "Avenida Getúlio Vargas", "Rua Tiradentes", "Rua Santos Dumont", "Rua Bandeirantes",
        "Rua das Palmeiras", "Rua do Comércio", "Avenida Central", "Rua Barão do Rio Branco", "Rua Marechal Deodoro"
    ];

    public static readonly string[] Districts =
    [
        "Centro", "Vila Nova", "Jardim das Acácias", "Vila Industrial", "Jardim Primavera",
        "Vila São João", "Jardim Europa", "Vila Operária", "Jardim América", "Vila Esperança"
    ];
}
