using System.Numerics;
using System.Text;
using Org.BouncyCastle.Asn1.X509;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;

namespace Phantasma.Business.Blockchain.Contracts
{
    public enum ConstraintKind
    {
        MaxValue,
        MinValue,
        GreatThanOther,
        LessThanOther,
        MustIncrease,
        MustDecrease,
        Deviation,
    }

    public struct ChainConstraint
    {
        public ConstraintKind Kind;
        public BigInteger Value;
        public string Tag;
    }

    public struct GovernancePair
    {
        public string Name;
        public BigInteger Value;
    }

    public sealed class GovernanceContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Governance;
        

#pragma warning disable 0649
        private StorageMap _valueMap;
        private StorageMap _constraintMap;
        private StorageList _nameList;
#pragma warning restore 0649

        public const string GasMinimumFeeTag = "governance.gas.minimumfee";
        
        public GovernanceContract() : base()
        {
        }

        public bool HasName(string name)
        {
            return HasValue(name);
        }

        public string[] GetNames()
        {
            return _nameList.All<string>();
        }

        public GovernancePair[] GetValues()
        {
            var names = GetNames();
            var result = new GovernancePair[names.Length];
            for (int i=0; i<result.Length; i++)
            {
                var name = names[i];
                result[i] = new GovernancePair()
                {
                    Name = name,
                    Value = GetValue(name)
                };
            }
            return result;
        }

        #region VALUES
        public bool HasValue(string name)
        {
            return _valueMap.ContainsKey<string>(name);
        }

        private void ValidateConstraints(string name, BigInteger previous, BigInteger current, ChainConstraint[] constraints, bool usePrevious)
        {
            for (int i=0; i<constraints.Length; i++)
            {
                var constraint = constraints[i];
                switch (constraint.Kind)
                {
                    case ConstraintKind.MustIncrease:
                        Runtime.Expect(!usePrevious || previous < current, "value must increase");
                        break;

                    case ConstraintKind.MustDecrease:
                        Runtime.Expect(!usePrevious || previous > current, "value must decrease");
                        break;

                    case ConstraintKind.MinValue:
                        Runtime.Expect(current >= constraint.Value, "value is too small");
                        break;

                    case ConstraintKind.MaxValue:
                        Runtime.Expect(current <= constraint.Value, "value is large small");
                        break;

                    case ConstraintKind.GreatThanOther:
                        {
                            Runtime.Expect(name != constraint.Tag, "other tag in constraint must have different name");
                            if (usePrevious)
                            {
                                var other = Runtime.GetGovernanceValue(constraint.Tag);
                                Runtime.Expect(current > other, "value is too small when compared to other");
                            }
                            break;
                        }

                    case ConstraintKind.LessThanOther:
                        {
                            Runtime.Expect(name != constraint.Tag, "other tag in constraint must have different name");
                            if (usePrevious)
                            {
                                var other = Runtime.GetGovernanceValue(constraint.Tag);
                                Runtime.Expect(current < other, "value is too big when compared to other");
                            }
                            break;
                        }

                    case ConstraintKind.Deviation:
                        {
                            Runtime.Expect(false, "deviation constraint not supported yet");
                            break;
                        }               
                 }
            }
        }

        public void CreateValue(Address from,  string name, BigInteger initial, byte[] serializedConstraints)
        {
            Runtime.Expect(!HasName(name), "name already exists");

            Runtime.Expect(!Nexus.IsDangerousAddress(from), "this address can't be used as source");

            Runtime.Expect(Runtime.IsPrimaryValidator(from), $"{from.TendermintAddress} is not a validator address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var constraints = Serialization.Unserialize<ChainConstraint[]>(serializedConstraints);
            ValidateConstraints(name, 0, initial, constraints, false);

            if (name == ValidatorContract.ValidatorSlotsTag && Runtime.NexusName == DomainSettings.NexusMainnet)
            {
                Runtime.Expect(initial == DomainSettings.InitialValidatorCount, $"The initial number of validators must always be {DomainSettings.InitialValidatorCount}.");
            }

            _valueMap.Set<string, BigInteger>(name, initial);
            _constraintMap.Set<string, ChainConstraint[]>(name, constraints);
            _nameList.Add<string>(name);

            Runtime.Notify(EventKind.ValueCreate, from, new ChainValueEventData() { Name = name, Value = initial });
        }

        //Optimized function in Nexus.OptimizedGetGovernanceValue
        public BigInteger GetValue(string name)
        {
            Runtime.Expect(HasValue(name), "invalid value name in GetValue");
            var value = _valueMap.Get<string, BigInteger>(name);
            return value;
        }

        public void SetValue(Address from, string name, BigInteger value)
        {
            Runtime.Expect(HasValue(name), "invalid value name in SetValue");

            // TODO this might not be necessary here since we check for consensus below
            Runtime.Expect(Runtime.IsPrimaryValidator(from), "must be validator address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            if (Runtime.ProtocolVersion <= 9)
            {
                var pollName = ConsensusContract.SystemPoll + name;
                var hasConsensus = Runtime.CallNativeContext(NativeContractKind.Consensus, nameof(ConsensusContract.HasConsensus), pollName, Encoding.UTF8.GetBytes(value.ToString())).AsBool();
                Runtime.Expect(hasConsensus, "consensus not reached");
            }
            else
            {
                string pollName = name;
                if (!name.Contains(ConsensusContract.SystemPoll))
                {
                    pollName = ConsensusContract.SystemPoll + name;
                }
                
                var rank = Runtime.CallNativeContext(NativeContractKind.Consensus, nameof(ConsensusContract.GetRank), pollName, Encoding.UTF8.GetBytes(value.ToString())).AsNumber();
                Runtime.Expect(rank == 0, "consensus not reached");
            }
            
            var previous = _valueMap.Get<string, BigInteger>(name);
            var constraints = _constraintMap.Get<string, ChainConstraint[]>(name);
            ValidateConstraints(name, previous, value, constraints, true);

            _valueMap.Set<string, BigInteger>(name, value);

            Runtime.Notify(EventKind.ValueUpdate, from, new ChainValueEventData() { Name = name, Value = value});
        }
        #endregion
    }
}
