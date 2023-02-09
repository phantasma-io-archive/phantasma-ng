using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Domain;
using Phantasma.Core.Storage.Context;

namespace Phantasma.Business.Blockchain
{
    public class Organization : IOrganization
    {
        public string ID { get; private set; }
        public string Name { get; private set; }

        private Address _address;
        public Address Address
        {
            get
            {
                if (_address.IsNull)
                {
                    _address = GetAddressFromID(ID);
                }

                return _address;
            }
        }

        public byte[] Script { get; private set; }

        public BigInteger Size => GetMemberList().Count();

        private StorageContext Storage;

        public Organization(string name, StorageContext storage)
        {
            this.Storage = storage;

            this.ID = name;

            var key = GetKey("script");
            if (Storage.Has(key))
            {
                this.Script = Storage.Get(key);
            }
            else
            {
                this.Script = null;
            }

            key = GetKey("name");
            if (Storage.Has(key))
            {
                this.Name = Storage.Get<string>(key);
            }
            else
            {
                this.Name = null;
            }
        }

        public void Init(string name, byte[] script)
        {
            this.Name = name;
            var key = GetKey("name");
            this.Storage.Put(key, name);

            this.Script = script;
            key = GetKey("script");
            this.Storage.Put(key, script);
        }
        
        public void InitCreator(Address creator)
        {
            var list = GetMemberList();
            list.Add<Address>(creator);
        }

        private byte[] GetKey(string key)
        {
            return System.Text.Encoding.UTF8.GetBytes($".org.{ID}.{key}");
        }

        private StorageList GetMemberList()
        {
            var key = GetKey("list");
            return new StorageList(key, this.Storage);
        }

        public Address[] GetMembers()
        {
            var list = GetMemberList();
            return list.All<Address>();
        }

        public bool IsMember(Address address)
        {
            var list = GetMemberList();
            return list.Contains<Address>(address);
        }

        public bool AddMember(IRuntime Runtime, Address from, Address target)
        {
            if (from.IsSystem)
            {
                if (Runtime.ProtocolVersion <= 9)
                {
                    Runtime.Expect(from != this.Address, "can't add organization as member of itself");
                }
                else
                {
                    Runtime.Expect(target != this.Address, "can't add organization as member of itself");
                }
            }

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var list = GetMemberList();

            if (list.Contains<Address>(target))
            {
                return false;
            }

            if (Runtime.ProtocolVersion >= 10)
            {
                if (Runtime.CurrentContext.Name != "stake" && Runtime.CurrentContext.Name != "validator")
                {
                    Runtime.ExpectWarning(from == this.Address, "Only organization can add members", from);
                    var orgMembers = GetMembers();
                    var numberOfSignaturesNeeded = (int)Math.Round(((decimal)(orgMembers.Length * 75) / 100));
                    if ( numberOfSignaturesNeeded <= 0 )
                    {
                        numberOfSignaturesNeeded = 1;
                    }
                    
                    Runtime.ExpectWarning(Runtime.Transaction.Signatures.Length >= numberOfSignaturesNeeded,
                        "must be signed by the majority org members", from);
                    
                    var msg = Runtime.Transaction.ToByteArray(false);
                    var validSignatures = 0;
                    Signature lastSignature = null;
                    var signatures = Runtime.Transaction.Signatures.ToList();
                    
                    foreach (var member in orgMembers )
                    {
                        foreach ( var signature in signatures )
                        {
                            if ( signature.Verify(msg, member) )
                            {
                                validSignatures++;
                                lastSignature = signature;
                                break;
                            }
                        }
                        if ( lastSignature != null)
                            signatures.Remove(lastSignature);
                    }

                    Runtime.ExpectWarning(validSignatures == numberOfSignaturesNeeded, "Number of valid signatures don't match", from);
                    
                    /*Runtime.ExpectWarning(Runtime.IsWitness(from), $"Trying to add member {target.Text} without witness from {from.Text}", from);
                    if (!list.Contains<Address>(from))
                    {
                        Runtime.ExpectWarning(false, $"Address {from.Text} is not a member of the DAO", from);
                        return false;
                    }*/
                }
            }

            list.Add<Address>(target);

            Runtime.Notify(EventKind.OrganizationAdd, from, new OrganizationEventData(this.ID, target));
            return true;
        }

        public bool RemoveMember(IRuntime Runtime, Address from, Address target)
        {
            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            var list = GetMemberList();

            if (!list.Contains<Address>(target))
            {
                return false;
            }
            
            if (Runtime.ProtocolVersion >= 10)
            {
                if (from.Text.Equals(target.Text))
                {
                    Runtime.ExpectWarning(Runtime.IsWitness(from), $"Address {from.Text} is not witness", from);
                }
                else if (Runtime.CurrentContext.Name != "stake" && Runtime.CurrentContext.Name != "validator")
                {
                    Runtime.ExpectWarning(from == this.Address, "Only organization can remove members", from);
                    var orgMembers = GetMembers();
                    
                    var numberOfSignaturesNeeded = (int)Math.Round(((decimal)(orgMembers.Length * 75) / 100));
                    if ( numberOfSignaturesNeeded <= 0 )
                    {
                        numberOfSignaturesNeeded = 1;
                    }
                    
                    Runtime.ExpectWarning(Runtime.Transaction.Signatures.Length >= numberOfSignaturesNeeded,
                        "must be signed by the majority org members", from);
                    
                    var msg = Runtime.Transaction.ToByteArray(false);
                    var validSignatures = 0;
                    Signature lastSignature = null;
                    var signatures = Runtime.Transaction.Signatures.ToList();
                    
                    foreach (var member in orgMembers )
                    {
                        foreach ( var signature in signatures )
                        {
                            if ( signature.Verify(msg, member) )
                            {
                                validSignatures++;
                                lastSignature = signature;
                                break;
                            }
                        }
                        if ( lastSignature != null)
                            signatures.Remove(lastSignature);
                    }

                    Runtime.ExpectWarning(validSignatures == numberOfSignaturesNeeded, "Number of valid signatures don't match", from);
                    
                    /*Runtime.ExpectWarning(Runtime.IsWitness(from),
                        $"Trying to remove member {target.Text} without witness from {from.Text}", from);
                    if (!list.Contains<Address>(from))
                    {
                        Runtime.ExpectWarning(false, $"Address {from.Text} is not a member of the DAO", from);
                        return false;
                    }*/
                }
            }

            list.Remove<Address>(target);

            Runtime.Notify(EventKind.OrganizationRemove, from, new OrganizationEventData(this.ID, target));
            return true;
        }

        public bool IsWitness(Transaction transaction)
        {
            var size = this.Size;
            if (size < 1)
            {
                return false;
            }

            var majorityCount = (size / 2) + 1;
            if (transaction == null || transaction.Signatures.Length < majorityCount)
            {
                return false;
            }

            int witnessCount = 0;

            var members = new List<Address>(this.GetMembers());
            var msg = transaction.ToByteArray(false);

            foreach (var sig in transaction.Signatures)
            {
                if (witnessCount >= majorityCount)
                {
                    break; // dont waste time if we already reached a majority
                }

                foreach (var addr in members)
                {
                    if (sig.Verify(msg, addr))
                    {
                        witnessCount++;
                        members.Remove(addr);
                        break;
                    }
                }
            }

            return witnessCount >= majorityCount;
        }

        public bool MigrateMember(IRuntime Runtime, Address admin, Address from, Address to)
        {

            Runtime.Expect(Runtime.IsRootChain(), "must be root chain");

            if (to.IsSystem)
            {
                Runtime.Expect(to!= this.Address, "can't add organization as member of itself");
            }

            var list = GetMemberList();

            if (!list.Contains<Address>(from))
            {
                return false;
            }

            Runtime.Expect(!list.Contains<Address>(to), "target address is already a member of organization");

            if ( Runtime.ProtocolVersion >= 10)
            {
                if (!admin.IsSystem)
                {
                    Runtime.ExpectWarning(Runtime.IsWitness(admin), $"Trying to migrate member {from.Text} without witness from {admin.Text}", admin);
                    if (!list.Contains<Address>(admin))
                    {
                        Runtime.CheckWarning(false, $"Address {admin.Text} is not a member of the DAO", admin);
                        return false;
                    }
                }
                else
                {
                    Runtime.ExpectWarning(Runtime.PreviousContext.Name == "validator" || Runtime.PreviousContext.Name == "account", "invalid context", admin);
                }
            }
            
            list.Remove<Address>(from);
            list.Add<Address>(to);

            Runtime.Notify(EventKind.OrganizationRemove, admin, new OrganizationEventData(this.ID, from));
            Runtime.Notify(EventKind.OrganizationAdd, admin, new OrganizationEventData(this.ID, to));

            return true;
        }

        public static Address GetAddressFromID(string ID)
        {
            return Address.FromHash(ID);
        }
    }
}
