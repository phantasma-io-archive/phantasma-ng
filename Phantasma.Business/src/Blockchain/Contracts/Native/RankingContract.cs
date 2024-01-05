using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.LeaderboardDetails;
using Phantasma.Core.Domain.Contract.LeaderboardDetails.Structs;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;

namespace Phantasma.Business.Blockchain.Contracts.Native
{
    public sealed class RankingContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Ranking;

#pragma warning disable 0649
        internal StorageMap _leaderboards; // name, Leaderboard
        internal StorageMap _rows; // name, List<LeaderboardEntry>
#pragma warning restore 0649

        public RankingContract() : base()
        {
        }

        /// <summary>
        /// Check's if a leaderboard exists.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool Exists(string name)
        {
            return _leaderboards.ContainsKey(name);
        }
        
        /// <summary>
        /// Method used to create a leaderboard
        /// </summary>
        /// <param name="from"></param>
        /// <param name="name"></param>
        /// <param name="size"></param>
        public void CreateLeaderboard(Address from, string name, BigInteger size)
        {
            Runtime.Expect(size >= 5, "size invalid");
            Runtime.Expect(size <= 1000, "size too large");

            Runtime.Expect(!from.IsInterop, "address cannot be interop");

            Runtime.Expect(!Exists(name), "leaderboard already exists");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(ValidationUtils.IsValidIdentifier(name), "invalid name");

            var leaderboard = new Leaderboard()
            {
                name = name,
                owner = from,
                size = size,
                round = 0,
            };
            _leaderboards.Set(name, leaderboard);

            Runtime.Notify(EventKind.LeaderboardCreate, from, name);
        }
        
        /// <summary>
        /// Method used to reset a leaderboard
        /// </summary>
        /// <param name="from"></param>
        /// <param name="name"></param>
        public void ResetLeaderboard(Address from, string name)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");
            var leaderboard = _leaderboards.Get<string, Leaderboard>(name);

            Runtime.Expect(from == leaderboard.owner, "invalid leaderboard owner");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            leaderboard.round++;

            var rows = _rows.Get<string, StorageList>(name);
            rows.Clear();

            if (Runtime.ProtocolVersion >= 19)
            {
                _leaderboards.Set(name, leaderboard);
            }
            
            Runtime.Notify(EventKind.LeaderboardReset, from, name);
        }

        /// <summary>
        /// Returns Leaderboard from a given name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Leaderboard GetLeaderboard(string name)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");
            return _leaderboards.Get<string, Leaderboard>(name);
        }

        /// <summary>
        /// Returns the number of rows for a given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public LeaderboardRow[] GetRows(string name)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");
            var rows = _rows.Get<string, StorageList>(name);

            return rows.All<LeaderboardRow>();
        }

        /// <summary>
        /// Returns the score of an address in a leaderboard.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public BigInteger GetScoreByAddress(string name, Address target)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();

            for (int i = 0; i < count; i++)
            {
                var entry = rows.Get<LeaderboardRow>(i);
                if (entry.address == target)
                {
                    return entry.score;
                }
            }

            return 0;
        }

        /// <summary>
        /// Returns the score of a given leaderboard index.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public BigInteger GetScoreByIndex(string name, BigInteger index)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();

            if (index < 0 || index >= count)
            {
                return 0;
            }

            var entry = rows.Get<LeaderboardRow>(index);
            return entry.score;
        }

        /// <summary>
        /// Returns the address for a given leaderboard index.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public Address GetAddressByIndex(string name, BigInteger index)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();

            if (index < 0 || index >= count)
            {
                return Address.Null;
            }

            var entry = rows.Get<LeaderboardRow>(index);
            return entry.address;
        }

        /// <summary>
        /// Returns the leaderboard size.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public BigInteger GetSize(string name)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var rows = _rows.Get<string, StorageList>(name);
            return rows.Count();
        }

        /// <summary>
        /// Method used to insert a score in the leaderboard.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="target"></param>
        /// <param name="name"></param>
        /// <param name="score"></param>
        public void InsertScore(Address from, Address target, string name, BigInteger score)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var leaderboard = _leaderboards.Get<string, Leaderboard>(name);

            Runtime.Expect(from == leaderboard.owner, "invalid leaderboard owner");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();
            var oldCount = count;

            int oldIndex = -1;
            for (int i = 0; i < count; i++)
            {
                var entry = rows.Get<LeaderboardRow>(i);
                if (entry.address == target)
                {
                    if (entry.score > score)
                    {
                        return;
                    }
                    oldIndex = i;
                    break;
                }
            }

            if (oldIndex >= 0)
            {
                count--;

                for (int i = oldIndex; i <= count - 1; i++)
                {
                    var entry = rows.Get<LeaderboardRow>(i + 1);
                    rows.Replace(i, entry);
                }

                rows.RemoveAt(count);
            }

            int bestIndex = 0;

            var lastIndex = (int)(count - 1);
            for (int i = lastIndex; i >= 0; i--)
            {
                var entry = rows.Get<LeaderboardRow>(i);
                if (entry.score >= score)
                {
                    bestIndex = i + 1;
                    break;
                }
            }

            if (bestIndex >= leaderboard.size)
            {
                rows = _rows.Get<string, StorageList>(name);
                count = rows.Count();
                for (int i = 0; i < count; i++)
                {
                    var entry = rows.Get<LeaderboardRow>(i);
                    Runtime.Expect(entry.score >= score, "leaderboard bug");
                }

                return;
            }

            /*for (int i = lastIndex; i > bestIndex; i--)
            {
                var entry = rows.Get<LeaderboardRow>(i - 1);
                rows.Replace<LeaderboardRow>(i, entry);
            }*/

            var newRow = new LeaderboardRow()
            {
                address = target,
                score = score
            };

            if (bestIndex < count)
            {
                if (count < leaderboard.size)
                {
                    rows.Add(newRow);
                    for (int i = (int)count; i > bestIndex; i--)
                    {
                        var entry = rows.Get<LeaderboardRow>(i - 1);
                        rows.Replace(i, entry);
                    }
                }

                rows.Replace(bestIndex, newRow);
            }
            else
            {
                Runtime.Expect(bestIndex == count, "invalid insertion index");
                rows.Add(newRow);
            }

            rows = _rows.Get<string, StorageList>(name);
            count = rows.Count();
            for (int i = 0; i < bestIndex; i++)
            {
                var entry = rows.Get<LeaderboardRow>(i);
                Runtime.Expect(entry.score >= score, "leaderboard bug");
            }

            Runtime.Expect(count >= oldCount, "leaderboard bug");

            Runtime.Notify(EventKind.LeaderboardInsert, target, newRow);
        }
    }
}
