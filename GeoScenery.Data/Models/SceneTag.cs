using System.ComponentModel.DataAnnotations;

namespace GeoScenery.Data.Models;

public class SceneTag
{
    public long Id { get; set; }

    public long SceneId { get; set; }

    public Scene Scene { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Tag { get; set; } = string.Empty;
}
