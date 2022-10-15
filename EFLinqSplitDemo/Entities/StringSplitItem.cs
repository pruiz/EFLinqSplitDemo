using System.ComponentModel.DataAnnotations;

namespace EFLinqSplitDemo.Entities
{
    public class StringSplitItem
    {
        [Key]
        public string Value { get; set; }
    }
}