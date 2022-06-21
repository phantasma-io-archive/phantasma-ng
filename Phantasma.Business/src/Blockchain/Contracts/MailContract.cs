﻿using Phantasma.Business.Storage;
using Phantasma.Core;
using Phantasma.Core.Context;
using System.Threading.Tasks;

namespace Phantasma.Business.Contracts
{
    public sealed class MailContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Mail;

#pragma warning disable 0649
        internal StorageMap _domainMap; //<string, Address>
        internal StorageMap _userMap; //<Address, string>
        internal StorageMap _domainUsers; //<Address, Collection<StorageEntry>>
#pragma warning restore 0649

        public MailContract() : base()
        {
        }

        public async Task PushMessage(Address from, Address target, Hash archiveHash)
        {
            Runtime.Expect(await Runtime.IsWitness(from), "invalid witness");

            var ownedDomain = GetUserDomain(from);
            Runtime.Expect(!string.IsNullOrEmpty(ownedDomain), $"{from} not associated with any domain");

            var targetDomain = GetUserDomain(target);
            Runtime.Expect(!string.IsNullOrEmpty(targetDomain), $"{target} not associated with any domain");
            Runtime.Expect(ownedDomain == targetDomain, $"{target} is associated with different domain: {targetDomain}");

            var archive = Runtime.GetArchive(archiveHash);
            Runtime.Expect(archive != null, $"mail archive does not exist: {archiveHash}");

            var encryption = archive.Encryption as SharedArchiveEncryption;
            Runtime.Expect(encryption != null, "mail archive using unsupported encryption mode");

            Runtime.Expect(encryption.Source == from, "mail archive not encrypted with correct source");
            Runtime.Expect(encryption.Destination == target, "mail archive not encrypted with correct destination");

            await Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.AddFile), from, target, archiveHash);
        }

        #region domains
        public bool DomainExists(string domainName)
        {
            return _domainMap.ContainsKey<string>(domainName);
        }

        public async Task RegisterDomain(Address from, string domainName)
        {
            Runtime.Expect(await Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "destination must be user address");

            Runtime.Expect(!DomainExists(domainName), "domain already exists");

            _domainMap.Set(domainName, from);

            JoinDomain(from, domainName);

            Runtime.Notify(EventKind.DomainCreate, from, domainName);
        }

        public async Task UnregisterDomain(string domainName)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            var from = _domainMap.Get<string, Address>(domainName);

            Runtime.Expect(await Runtime.IsWitness(from), "invalid witness");

            _domainMap.Remove(domainName);

            Runtime.Notify(EventKind.DomainDelete, from, domainName);
        }

        public async Task MigrateDomain(string domainName, Address target)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            var currentDomain = GetUserDomain(target);
            Runtime.Expect(string.IsNullOrEmpty(currentDomain), "already associated with domain: " + currentDomain);

            var from = _domainMap.Get<string, Address>(domainName);

            Runtime.Expect(await Runtime.IsWitness(from), "invalid witness");

            _domainMap.Set(domainName, target);

            var users = GetDomainUsers(domainName);

            await Parallel.ForEachAsync(users, async (user, canceltoken) =>
                await Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.MigratePermission), user, from, target));
            

            Runtime.Notify(EventKind.AddressMigration, from, target);
        }

        public Address[] GetDomainUsers(string domainName)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            var list = _domainUsers.Get<string, StorageList>(domainName);
            return list.All<Address>();
        }

        public async Task JoinDomain(Address from, string domainName)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            Runtime.Expect(await Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "destination must be user address");

            var currentDomain = GetUserDomain(from);
            Runtime.Expect(string.IsNullOrEmpty(currentDomain), "already associated with domain: " + currentDomain);

            _userMap.Set(from, domainName);
            var list = _domainUsers.Get<string, StorageList>(domainName);
            list.Add(from);

            Runtime.Notify(EventKind.AddressRegister, from, domainName);
        }

        public async Task LeaveDomain(Address from, string domainName)
        {
            Runtime.Expect(DomainExists(domainName), "domain does not exist");

            Runtime.Expect(await Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(from.IsUser, "destination must be user address");

            var currentDomain = GetUserDomain(from);
            Runtime.Expect(currentDomain == domainName, "not associated with domain: " + domainName);

            _userMap.Remove(from);

            var list = _domainUsers.Get<string, StorageList>(domainName);
            list.Remove(from);

            Runtime.Notify(EventKind.AddressUnregister, from, domainName);
        }

        public string GetUserDomain(Address target)
        {
            if (_userMap.ContainsKey(target))
            {
                return _userMap.Get<Address, string>(target);
            }

            return null;
        }
        #endregion

        /*#region groups
        public void registerGroup(domain, name, storage_size)
        {

        }

        public void unregisterGroup(domain, name)
        {

        }

        public void joinGroup(target, domain, group)
        {

        }

        public void leaveGroup(target, domain, group)
        {

        }

        public void getGroupUsers(target)
        {

        }
        public string[] getDomainGroups(string domainName)
        {

        }



        #endregion*/
    }
}
