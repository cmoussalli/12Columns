using TwelveColumns;

namespace TwelveColumns.Tests;

public class GridLayoutSerializerTests
{
    [Fact]
    public void Round_trip_preserves_the_layout()
    {
        List<GridItem> original =
        [
            new()
            {
                Id = "traffic", Title = "Traffic", Type = "line",
                X = 3, Y = 2, W = 6, H = 4,
                MinW = 3, MinH = 2, MaxW = 12, MaxH = 8,
                Static = true, CssClass = "accent",
                Settings = { ["range"] = "24h" },
            },
            new() { Id = "mix", Title = "Mix", Type = "donut", X = 9, Y = 2, W = 3, H = 4 },
        ];

        var restored = GridLayoutSerializer.Deserialize(GridLayoutSerializer.Serialize(original));

        Assert.Equal(2, restored.Count);

        var traffic = restored[0];
        Assert.Equal("traffic", traffic.Id);
        Assert.Equal("Traffic", traffic.Title);
        Assert.Equal("line", traffic.Type);
        Assert.Equal((3, 2, 6, 4), (traffic.X, traffic.Y, traffic.W, traffic.H));
        Assert.Equal((3, 2), (traffic.MinW, traffic.MinH));
        Assert.Equal((12, 8), (traffic.MaxW, traffic.MaxH));
        Assert.True(traffic.Static);
        Assert.Equal("accent", traffic.CssClass);
        Assert.Equal("24h", traffic.Settings["range"]);
    }

    [Fact]
    public void Host_owned_data_is_not_serialized()
    {
        List<GridItem> original = [new() { Id = "a", Data = new object() }];

        var json = GridLayoutSerializer.Serialize(original);

        Assert.DoesNotContain("Data", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(GridLayoutSerializer.Deserialize(json)[0].Data);
    }

    [Fact]
    public void Deserialize_repairs_degenerate_sizes()
    {
        var restored = GridLayoutSerializer.Deserialize("""[{ "Id": "a", "X": 0, "Y": 0, "W": 0, "H": -3 }]""");

        Assert.Equal(1, restored[0].W);
        Assert.Equal(1, restored[0].H);
    }

    [Fact]
    public void Deserialize_assigns_an_id_when_one_is_missing()
    {
        var restored = GridLayoutSerializer.Deserialize("""[{ "X": 0, "Y": 0, "W": 2, "H": 2 }]""");

        Assert.False(string.IsNullOrWhiteSpace(restored[0].Id));
    }

    [Fact]
    public void An_empty_layout_round_trips()
    {
        Assert.Empty(GridLayoutSerializer.Deserialize(GridLayoutSerializer.Serialize([])));
    }
}
