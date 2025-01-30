using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PluginManagerObs.Models
{
    public class OBSPath
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OBSPathId { get; set; }
        public string Path { get; set; }

        public virtual ICollection<Plugin> Plugins { get; set; }
    }
}
