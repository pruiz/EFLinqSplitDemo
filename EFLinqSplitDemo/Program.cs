using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Objects;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using Colors.Net;
using BlazarTech.QueryableValues;
using Colors.Net.StringColorExtensions;
using EF6TempTableKit.Extensions;
using EFLinqSplitDemo.Entities;

using static Colors.Net.StringStaticMethods;

namespace EFLinqSplitDemo
{
    internal class Program
    {
        private static readonly int[] PaddingSlots = new[] { 10, 25, 50, 100, 250, 500 };
        private static readonly RichString CaseBeginPrefix = DarkBlue(">>> ");
        private static readonly RichString CaseEndPrefix = DarkBlue("<<< ");
        private static readonly RichString CaseSumPrefix = DarkMagenta("==> ");
        private static IEnumerable<Guid> Ids = null; //< To be filled with some existing guids on context creation..

        private static Database GetDatabase(ref IEnumerable<Guid> ids, int numids)
        {
            var result = new Database();
            var count = result.Items.Count();
            ids = result.Items.Select(x => x.Id).OrderBy(x => Guid.NewGuid()).Take(numids).ToArray();
            //result.Items.First(); //< XXX: Force context initialization before enabling logging..
            result.Database.Log = x =>
            {
                // Let's do some fancy printing..
                object prefix = "\t>> ".DarkYellow();
                foreach (var line in x.TrimEnd('\r', '\n').Split('\n'))
                {
                    ColoredConsole.Write(prefix);
                    var txt = line.TrimEnd('\r'); //< Have WriteLine handle right newline on each platform
                    if (txt.StartsWith("/*")) ColoredConsole.WriteLine(txt.Magenta());
                    else if (txt.StartsWith("-- ")) ColoredConsole.WriteLine(txt.Cyan());
                    else ColoredConsole.WriteLine(txt.Yellow());
                    prefix = "\t   "; //< Apply diff indent from second+ lines..
                }
            };
            return result;
        }
        
        private static int GetNearestPaddingFor(int count)
        {
            foreach (var slot in  PaddingSlots)
            {
                if (count <= slot) return (slot - count);
            }

            throw new NotSupportedException($"Too many elements for contains query: {count}");
        }

        private static Item[] GetAllNoFilter(Database db)
        {
            return db.Items.ToArray(); //< XXX: Not using .Count() on purpose..
        }

        private static Item[] GetTwoUsingContains(Database db)
        {
            var ids = Ids.Take(2).ToArray();
            return db.Items.Where(x => ids.Contains(x.Id)).ToArray();
        }

        private static Item[] GetTwoUsingPaddedContains(Database db)
        {
            var ids = Ids.Take(2).ToArray();
            var padcount = GetNearestPaddingFor(ids.Length);
            ids = ids.Union(Enumerable.Range(0, padcount).Select(x => ids.Last()).ToArray()).ToArray();
            return db.Items.Where(x => ids.Contains(x.Id)).ToArray();
        }
        
        private static Item[] GetSixteenUsingPaddedContains(Database db)
        {
            var ids = Ids.Take(16).ToArray();
            var padcount = GetNearestPaddingFor(ids.Length);
            ids = ids.Union(Enumerable.Range(0, padcount).Select(x => ids.Last()).ToArray()).ToArray();
            return db.Items.Where(x => ids.Contains(x.Id)).ToArray();
        }
        
        private static Item[] GetItemsUsingSqlQueryWithContains(Database db)
        {
            var ids = string.Join(",", Ids.Take(4));
            var inner = db.Database.SqlQuery<Guid>(
                @"SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM STRING_SPLIT(@ids, ',')",
                new SqlParameter("@ids", ids)
            );
            return db.Items.Where(x => inner.Contains(x.Id)).ToArray();
        }
        
        private static Item[] GetItemsUsingSqlQueryWithAny(Database db)
        {
            var ids = string.Join(",", Ids.Take(4));
            var inner = db.Database.SqlQuery<Guid>(
                @"SELECT CAST([value] AS UNIQUEIDENTIFIER) FROM STRING_SPLIT(@ids, ',')",
                new SqlParameter("@ids", ids)
            );
            return db.Items.Where(x => inner.Any(i => i == x.Id)).ToArray();
        }

        private static Item[] GetItemsUsingSplitString(Database db)
        {
            var ids = string.Join(",", Ids.Take(4).Select(x => x.ToString()));
            return db.Items.Where(x => 
                    db.StringSplit(ids, ",").Any(i => new Guid(i.Value) == x.Id)
                ).ToArray();
        }

        private static Item[] GetItemsUsingAsQueryableValues(Database db)
        {
            var ids = Ids.Take(4).ToArray();
            var qids = db.AsQueryableValues(ids);
            return db.Items.Where(x => qids.Contains(x.Id)).ToArray();
        }
        
        private static Item[] GetItemsUsingTempTableWithAny(Database db)
        {
            var ids = Ids.Take(4).Select(x => new TempGuid(x));
            var qdb = db.WithTempTableExpression<Database>(ids);
            var result = qdb.Items.Where(x => qdb.TempGuids.Any(g => x.Id == g.Id)).ToArray();
            qdb.ReinitializeTempTableContainer();
            return result;
        }
        
        private static Item[] GetItemsUsingTempTableWithContains(Database db)
        {
            var ids = Ids.Take(4).Select(x => new TempGuid(x));
            var qdb = db.WithTempTableExpression<Database>(ids);
            var result = qdb.Items.Where(x => qdb.TempGuids.Select(g => g.Id).Contains(x.Id)).ToArray();
            qdb.ReinitializeTempTableContainer();
            return result;
        }
        
        private static Item[] GetItemsUsingTempTableWithJoin(Database db)
        {
            var ids = Ids.Take(4).Select(x => new TempGuid(x));
            var qdb = db.WithTempTableExpression<Database>(ids);
            var result = qdb.TempGuids.Join(
                qdb.Items.Where(x => true), //< XXX: Add additional filtering criteria here..
                g => g.Id,
                i => i.Id,
                (g, i) => i
            ).ToArray();
            qdb.ReinitializeTempTableContainer();
            return result;
        }
        
        private static void RunCase(string story, Func<Item[]> @delegate)
        {
            ColoredConsole.Write(CaseBeginPrefix).WriteLine(Blue(story + ".."));
            
            var sw = new Stopwatch();
            sw.Start();
            var items = @delegate();
            sw.Stop();
            
            ColoredConsole
                .Write(CaseEndPrefix).WriteLine($"{Blue(story + "..")} {Green("done!")}")
                .Write(CaseSumPrefix).WriteLine($"{Magenta("Took: " + sw.Elapsed)} {Magenta("(Rows: " + items.Length + ")")}")
                .WriteLine();
        }

        public static void Main(string[] args)
        {
            ColoredConsole.Write(CaseBeginPrefix).WriteLine("Initializing database context..".Blue());
            var db = GetDatabase(ref Ids, 400);
            ColoredConsole.Write(CaseEndPrefix).Write(Blue("Initializing database context.. ")).WriteLine(Green("done!")).WriteLine();

            RunCase("Selecting all items' w/o filtering", () => GetAllNoFilter(db));
            RunCase("Selecting some items by id, using Contains()", () => GetTwoUsingContains(db));
            RunCase("Selecting some items by id, using Contains() with padding", () => GetTwoUsingPaddedContains(db));
            RunCase("Selecting some more items by id, using Contains() with padding", () => GetSixteenUsingPaddedContains(db)); 
            RunCase("Selecting some items by id, using an SqlQuery w/ Any", () => GetItemsUsingSqlQueryWithAny(db));
            RunCase("Selecting some items by id, using an SqlQuery w/ Contains", () => GetItemsUsingSqlQueryWithContains(db));
            RunCase("Selecting some items by id, using Contains+StringSplit", () => GetItemsUsingSplitString(db));
            RunCase("Selecting some items by id, using AsQueryableValues", () => GetItemsUsingAsQueryableValues(db));
            RunCase("Selecting some items by id, using a Temporary Table w/ Any", () => GetItemsUsingTempTableWithAny(db));
            RunCase("Selecting some items by id, using a Temporary Table w/ Contains", () => GetItemsUsingTempTableWithContains(db));
            RunCase("Selecting some items by id, using a Temporary Table w/ Join", () => GetItemsUsingTempTableWithJoin(db));
        }
    }
}