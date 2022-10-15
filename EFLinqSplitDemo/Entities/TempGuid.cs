using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EF6TempTableKit.Attributes;

namespace EFLinqSplitDemo.Entities
{
    [Table("#TempGuids")]
    public class TempGuid : ITempTable
    {
        [Key]
        [TempFieldType("uniqueidentifier")]
        [StringConverter("'{0:D}'")]
        public Guid Id { get; set; }

        public TempGuid(Guid id)
        {
            Id = id;
        }
    }
}