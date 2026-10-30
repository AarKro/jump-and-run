using UnityEngine;

// Marktpromenade an der Marinara Promenade zwischen Crust Street und Salami Street (Level 4 fährt hier auf dem Skateboard
// durch die Menge). Statt der Ringstraße liegt dort ein Plattenweg auf Gehweghöhe von der Blockkante bis zur Kaimauer,
// an der Wasserkante ein Geländer (mit Lücken an den Stegen des Jachthafens), die Four Cheese Avenue endet mit Pollern.
// Die Kamera steht dort über dem Wasser und schaut über die Promenade auf Stände und Häuser.
// Der Plattenweg ist CityGround (achsenparallel, Level können ihn ausschneiden).
public static partial class CityBuilder
{
    private static void BuildMarketPromenade()
    {
        Transform market = Group("Market Promenade");
        const float z0 = CityLayout.MarketFromZ, z1 = CityLayout.MarketToZ;
        // 5 cm über die Kaimauer hinaus: Lägen beide Seitenflächen genau übereinander, flackerten sie (Z-Fighting)
        const float x0 = CityLayout.MarketWaterX - 0.05f, x1 = CityLayout.MarketBlockEdgeX;

        GameObject walk = Slab("Market Paving", market, x0, z0, x1, z1, Surface - 1f, Surface, PavingMaterial()).gameObject;
        walk.layer = GroundLayer;
        walk.AddComponent<BoxCollider>();
        walk.AddComponent<CityGround>();

        BuildQuayRailing(market, x0 + 0.25f, z0, z1);
        BuildFourCheeseBollards(market);
    }

    // Geländer an der Wasserkante: Pfosten alle 2 m, zwei Holme. Offen, wo die Stege des Jachthafens anlegen
    private static void BuildQuayRailing(Transform parent, float x, float z0, float z1)
    {
        Material iron = Mat("Iron");
        Material white = Mat("Awning White");
        Transform railing = new GameObject("Quay Railing").transform;
        railing.SetParent(parent, false);

        float[] jetties = { 150f, 200f };
        const float jettyHalf = 2.5f;
        bool Open(float z) => System.Array.Exists(jetties, j => Mathf.Abs(z - j) < jettyHalf);

        float from = z0 + 0.5f;
        float z = from;
        while (z <= z1 - 0.5f)
        {
            if (!Open(z))
            {
                PrimitiveKit.Cylinder("Post", railing, new Vector3(x, Surface + 0.55f, z), 0.05f, 1.1f, iron);
            }
            float next = z + 2f;
            // Holme bis zum nächsten Pfosten, außer über einer Stegöffnung
            if (next <= z1 - 0.5f && !Open(z) && !Open(next))
            {
                PrimitiveKit.Box("Rail", railing, new Vector3(x, Surface + 1.05f, z + 1f), new Vector3(0.08f, 0.08f, 2f), white);
                PrimitiveKit.Box("Rail Low", railing, new Vector3(x, Surface + 0.55f, z + 1f), new Vector3(0.05f, 0.05f, 2f), iron);
            }
            z = next;
        }
    }

    // Die Four Cheese Avenue endet vor der Promenade: weiß-rote Poller quer über die Fahrbahn an der Blockkante
    private static void BuildFourCheeseBollards(Transform parent)
    {
        Material white = Mat("Awning White");
        Material red = Mat("Pizzeria Red");
        Transform bollards = new GameObject("Four Cheese Bollards").transform;
        bollards.SetParent(parent, false);
        const float x = CityLayout.MarketBlockEdgeX + 0.6f;
        for (float z = 215f - 5.5f; z <= 215f + 5.5f; z += 1.25f)
        {
            PrimitiveKit.Cylinder("Bollard", bollards, new Vector3(x, RoadTop + 0.45f, z), 0.11f, 0.9f, white);
            PrimitiveKit.Cylinder("Band", bollards, new Vector3(x, RoadTop + 0.7f, z), 0.115f, 0.12f, red);
        }
    }
}
