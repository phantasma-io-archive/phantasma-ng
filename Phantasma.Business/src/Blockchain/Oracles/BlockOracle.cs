﻿using System;
using System.Collections.Generic;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Types;

namespace Phantasma.Business.Blockchain
{
    public class BlockOracleReader : OracleReader
    {
        public readonly Block OriginalBlock;

        public BlockOracleReader(Nexus nexus, Block block) : base(nexus)
        {
            this.OriginalBlock = block;

            foreach (var entry in block.OracleData)
            {
                var oEntry = (OracleEntry)entry;
                _entries[entry.URL] = oEntry;
            }
        }

        public override T Read<T>(Timestamp time, string url) 
        {
            T content = null;


            string tag = url;

            foreach(KeyValuePair<string, OracleEntry> entry in _entries)
            {
                if (string.Equals(entry.Key, tag))
                {
                    content = entry.Value.Content as T;
                    break;
                }
            }

            return content;
        }

        public override List<InteropBlock> ReadAllBlocks(string platformName, string chainName)
        {
            throw new NotImplementedException();
        }

        public override string GetCurrentHeight(string platformName, string chainName)
        {
            throw new NotImplementedException();
        }

        public override void SetCurrentHeight(string platformName, string chainName, string height)
        {
            throw new NotImplementedException();
        }

        protected override InteropBlock PullPlatformBlock(string platformName, string chainName, Hash hash, BigInteger height = new BigInteger())
        {
            throw new NotImplementedException();
        }

        protected override BigInteger PullFee(Timestamp time, string platform)
        {
            throw new NotImplementedException();
        }

        protected override decimal PullPrice(Timestamp time, string symbol)
        {
            throw new NotImplementedException();
        }

        protected override InteropTransaction PullPlatformTransaction(string platformName, string chainName, Hash hash)
        {
            throw new NotImplementedException();
        }

        protected override InteropNFT PullPlatformNFT(string platformName, string symbol, BigInteger tokenID)
        {
            throw new NotImplementedException();
        }

        protected override T PullData<T>(Timestamp time, string url)
        {
            throw new NotImplementedException();
        }

        public new void Clear()
        {
            throw new NotImplementedException();
        }
    }
}
