using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Phantasma.Core;
using Phantasma.Core.Storage;
using RocksDbSharp;
using Serilog;

namespace Phantasma.Infrastructure.RocksDB
{
    public class DBPartition : IKeyValueStoreAdapter
    {
	    private RocksDb _db;
        private ColumnFamilyHandle partition;
        private string partitionName;
        private string path;

        public uint Count => GetCount();

        public DBPartition(string fileName)
        {
            this.partitionName = Path.GetFileName(fileName);
            this.path = Path.GetDirectoryName(fileName);

            if (string.IsNullOrEmpty(path))
            {
                throw new Exception("Rocksdb storage path was not configured properly");
            }

            if (!path.EndsWith("/"))
            {
                path += '/';
            }

            Log.Information($"RocksDB partition path: {fileName}");

            this._db = RocksDbStore.Instance(path);
            Log.Information($"RocksDB partition path: {fileName}");

            // Create partition if it doesn't exist already
            try
            {
                Log.Information("Getting partition: " + this.partitionName);
                this.partition = this._db.GetColumnFamily(partitionName);
            }
            catch
            {
                Log.Information("Partition not found, create it now: " + this.partitionName);
                var cf = new ColumnFamilyOptions();
                // TODO different partitions might need different options...
                this.partition = this._db.CreateColumnFamily(cf, partitionName);
            }
        }

        #region internal_funcs
        private ColumnFamilyHandle GetPartition()
        {
            return _db.GetColumnFamily(partitionName);
        }

        private byte[] Get_Internal(byte[] key)
        {
            return _db.Get(key, cf: GetPartition());
        }

        private void Put_Internal(byte[] key, byte[] value)
        {
            _db.Put(key, value, cf: GetPartition());
        }

        private void Remove_Internal(byte[] key)
        {
            _db.Remove(key, cf: GetPartition());
        }
        #endregion

        public void CreateCF(string name)
        {
            var cf = new ColumnFamilyOptions();
            _db.CreateColumnFamily(cf, name);
        }

        public uint GetCount()
        {
            uint count = 0;
            var readOptions = new ReadOptions();
            using (var iter = _db.NewIterator(readOptions: readOptions, cf: GetPartition()))
            {
                iter.SeekToFirst();
                while (iter.Valid())
                {
                    count++;
                    iter.Next();
                }
            }

            return count;
        }

        public void Visit(Action<byte[], byte[]> visitor, ulong searchCount = 0, byte[] prefix = null)
        {
            var readOptions = new ReadOptions().SetPrefixSameAsStart(true);
            using (var iter = _db.NewIterator(readOptions: readOptions, cf: GetPartition()))
            {
                if (prefix == null || prefix.Length == 0)
                {
                    iter.SeekToFirst();
                    while (iter.Valid())
                    {
                        visitor(iter.Key(), iter.Value());
                        iter.Next();
                    }
                }
                else
                {
                    ulong _count = 0;
                    iter.Seek(prefix);
                    while (iter.Valid() && _count < searchCount)
                    {
                        visitor(iter.Key(), iter.Value());
                        iter.Next();
                        _count++;
                    }
                }
            }
        }

        public void SetValue(byte[] key, byte[] value)
        {
            Throw.IfNull(key, nameof(key));
            Put_Internal(key, value);
        }

        public byte[] GetValue(byte[] key)
        {
            byte[] value;
            if (ContainsKey_Internal(key, out value))
            {
                return value;
            }

            return null;
        }

        public bool ContainsKey_Internal(byte[] key, out byte[] value)
        {
            value = Get_Internal(key);
            return (value != null) ? true : false;
        }

        public bool ContainsKey(byte[] key)
        {
            var value = Get_Internal(key);
            return (value != null) ? true : false;
        }

        public void Remove(byte[] key)
        {
            Remove_Internal(key);
        }
    }

    public class RocksDbStore
    {
	    private static ConcurrentDictionary<string, RocksDb> _db = new ConcurrentDictionary<string, RocksDb>();
	    private static ConcurrentDictionary<string, RocksDbStore> _rdb = new ConcurrentDictionary<string, RocksDbStore>();

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
