using System.Linq;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.Interception;
using System.Reflection;
using CodeFirstStoreFunctions;
using EF6TempTableKit.DbContext;

namespace EFLinqSplitDemo.Entities
{
    [DbConfigurationType(typeof(EF6TempTableKitDbConfiguration))]
    public class Database : DbContext, IDbContextWithTempTable
    {
        private ObjectContext ObjectContext => ((IObjectContextAdapter)this).ObjectContext;
        
        public TempTableContainer TempTableContainer { get; set; } = new TempTableContainer();
        public DbSet<Item> Items { get; set; }
        public DbSet<TempGuid> TempGuids { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Conventions.Add(new FunctionsConvention<Database>("dbo"));
            modelBuilder.ComplexType<StringSplitItem>();
        }
        
        [DbFunction(nameof(Database), "STRING_SPLIT")]
        [DbFunctionDetails(IsBuiltIn = true)]
        public IQueryable<StringSplitItem> StringSplit(string @string, string separator)
        {
            var str = !string.IsNullOrWhiteSpace(@string)
                ? new ObjectParameter("string", @string)
                : new ObjectParameter("string", typeof(string));
            var sep = new ObjectParameter("separator", separator);

            return ObjectContext.CreateQuery<StringSplitItem>(
                $"STRING_SPLIT(@{nameof(@string)}, @{nameof(separator)})", str, sep
            );
        }
    }
}