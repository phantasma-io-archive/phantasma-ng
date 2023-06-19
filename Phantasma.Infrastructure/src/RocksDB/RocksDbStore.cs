using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using RocksDbSharp;
using Serilog;

namespace Phantasma.Infrastructure.RocksDB
{
    public class RocksDbStore
    {
        private static ConcurrentDictionary<string, RocksDb> _db = new ConcurrentDictionary<string, RocksDb>();

        private static ConcurrentDictionary<string, RocksDbStore> _rdb =
            new ConcurrentDictionary<string, RocksDbStore>();

        private string fileName;

        private RocksDbStore(string fileName)
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
            this.fileName = fileName.Replace("\\", "/");

            var path = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //TODO check options
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            var columnFamilies = new ColumnFamilies
            {
                { "default", new ColumnFamilyOptions().OptimizeForPointLookup(256) },
            };

            try
            {
                var partitionList = RocksDb.ListColumnFamilies(options, path);

                foreach (var partition in partitionList)
                {
                    columnFamilies.Add(partition, new ColumnFamilyOptions());
                }
            }
            catch
            {
                Log.Warning("Inital start, no partitions created yet!");
            }

            Log.Information("Opening database at: " + path);
            _db.TryAdd(fileName, RocksDb.Open(options, path, columnFamilies));
        }

        public static RocksDb Instance(string name)
        {
            //Log.Information("sdkflklsdfdklsafjkl");
            if (!_db.ContainsKey(name))
            {
                if (string.IsNullOrEmpty(name)) throw new System.ArgumentException("Parameter cannot be null", "name");

                _rdb.TryAdd(name, new RocksDbStore(name));
            }

            return _db[name];
        }

        private void Shutdown()
        {
            if (_db.Count > 0)
            {
                var toRemove = new List<String>();
                Log.Information($"Shutting down databases...");
                foreach (var db in _db)
                {
                    db.Value.Dispose();
                    toRemove.Add(db.Key);
                }

                foreach (var key in toRemove)
                {
                    _db.Remove(key, out RocksDb _);
                }

                Log.Information("Databases shut down!");
            }
        }
    }
}
