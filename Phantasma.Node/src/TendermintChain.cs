using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Transactions;
using Phantasma.Business.Blockchain;
using Phantasma.Business.Blockchain.Contracts;
using Phantasma.Business.Blockchain.Contracts.Native;
using Phantasma.Business.Blockchain.Tokens;
using Phantasma.Business.Blockchain.Tokens.Structs;
using Phantasma.Business.Blockchain.VM;
using Phantasma.Business.VM.Utils;
using Phantasma.Core;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Contract;
using Phantasma.Core.Domain.Contract.Enums;
using Phantasma.Core.Domain.Contract.Interop;
using Phantasma.Core.Domain.Contract.Interop.Structs;
using Phantasma.Core.Domain.Contract.Validator;
using Phantasma.Core.Domain.Contract.Validator.Enums;
using Phantasma.Core.Domain.Events;
using Phantasma.Core.Domain.Events.Structs;
using Phantasma.Core.Domain.Exceptions;
using Phantasma.Core.Domain.Execution;
using Phantasma.Core.Domain.Execution.Enums;
using Phantasma.Core.Domain.Interfaces;
using Phantasma.Core.Domain.Serializer;
using Phantasma.Core.Domain.Tasks;
using Phantasma.Core.Domain.Tasks.Enum;
using Phantasma.Core.Domain.Token;
using Phantasma.Core.Domain.Token.Enums;
using Phantasma.Core.Domain.TransactionData;
using Phantasma.Core.Domain.Validation;
using Phantasma.Core.Domain.VM;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Storage.Context.Structs;
using Phantasma.Core.Types;
using Phantasma.Core.Types.Structs;
using Phantasma.Core.Utils;
using Serilog;
using Serilog.Core;
using Tendermint.Abci;
using Tendermint.Crypto;

using Event = Phantasma.Core.Domain.Structs.Event;
using Transaction = Phantasma.Core.Domain.TransactionData.Transaction;

namespace Phantasma.Node
{
    public class TendermintChain: Chain
    {
        public TendermintChain(INexus nexus, string name) : base(nexus, name) { }

        private List<ValidatorUpdate> HandleValidatorUpdates()
        {
            if (!this.Nexus.HasGenesis()) return new List<ValidatorUpdate>();
            var validators = this.Nexus.GetValidators(this.CurrentBlock.Timestamp);
            if (validators.Length == 0) return new List<ValidatorUpdate>();
            if (this.Nexus.GetProtocolVersion(this.Storage) <= 14) return new List<ValidatorUpdate>();

            var validatorUpdateList = new List<ValidatorUpdate>();
            Timestamp lastActivity;
            uint timeDiference = uint.Parse(this.Nexus.GetGovernanceValue(this.Storage, ValidatorContract.ValidatorMaxOfflineTimeTag).ToString());
            var validatorUpdate = new ValidatorUpdate();

            foreach (var validator in validators)
            {
                Log.Information("Validator {validator}", validator);
                validatorUpdate = new ValidatorUpdate();
                lastActivity = this.InvokeContractAtTimestamp(this.Storage, this.CurrentTime,
                    NativeContractKind.Validator, nameof(ValidatorContract.GetValidatorLastActivity),
                    validator.address).AsTimestamp();

                string jsonValidator = "{\"ed25519\":\"" + Convert.ToBase64String(Encoding.UTF8.GetBytes(validator.address.TendermintAddress)) + "\"}";
                PublicKey validatorUpdatePubKey = PublicKey.Parser.ParseJson(jsonValidator);

                if (lastActivity == Timestamp.Null)
                {
                    validatorUpdate.Power = 1;
                    validatorUpdate.PubKey = validatorUpdatePubKey;
                    validatorUpdateList.Add(validatorUpdate);

                    this.Nexus.RegisterValidatorActivity(this.Storage, validator.address,
                        this.CurrentBlock.Validator, this.CurrentTime, lastActivity);
                    continue;
                }

                if (lastActivity.Value + timeDiference > this.CurrentBlock.Timestamp)
                {
                    validatorUpdate.Power = 1;
                    validatorUpdate.PubKey = validatorUpdatePubKey;
                    validatorUpdateList.Add(validatorUpdate);
                    this.Nexus.RegisterValidatorActivity(this.Storage, validator.address,
                        this.CurrentBlock.Validator, this.CurrentTime, lastActivity);
                    continue;
                }

                validatorUpdate.Power = 0;
                validatorUpdate.PubKey = validatorUpdatePubKey;
                validatorUpdateList.Add(validatorUpdate);
                this.Nexus.DemoteValidator(this.Storage, validator.address,
                    this.CurrentBlock.Validator, this.CurrentTime, lastActivity);
            }

            return validatorUpdateList;
        }
    }
}
