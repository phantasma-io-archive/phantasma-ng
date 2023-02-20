using System.Numerics;
using System.Text;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Numerics;
using Phantasma.Core.Storage.Context;
using Phantasma.Core.Utils;

namespace Phantasma.Business.Blockchain.Storage;

public class TaskSheet
{
    public const string TaskInfoTag = "task_info.";
    public const string TaskRunTag = "task_run.";
    private const string TaskListTag = ".tasks";
    
    private byte[] _prefix;
    private string _taskName;
    private BigInteger _taskID;
    
    public TaskSheet(BigInteger taskID)
    {
        this._taskID = taskID;
        this._prefix = MakePrefix(taskID);
    }

    private byte[] GetTaskKey(BigInteger taskID, string field)
    {
        var bytes = ByteArrayUtils.ConcatBytes(Encoding.ASCII.GetBytes(field), taskID.ToUnsignedByteArray());
        var key = ByteArrayUtils.ConcatBytes(_prefix, bytes);
        return key;
    }
    
    public static byte[] MakePrefix(BigInteger taskID)
    {
        var key = $"{TaskListTag}.";
        return ByteArrayUtils.ConcatBytes(Encoding.UTF8.GetBytes(key), taskID.ToUnsignedByteArray());
    }

    #region Has
    public bool Has(StorageContext storage, byte[] key)
    {
        return storage.Has(key);
    }
    
    public bool HasTaskInfo(StorageContext storage)
    {
        return storage.Has(this.GetTaskInfoKey());
    }

    public bool HasTaskRun(StorageContext storage)
    {
        return storage.Has(this.GetTaskRunKey());
    }
    #endregion
    
    #region Put
    public bool Put(StorageContext storage, byte[] key, byte[] value)
    {
        lock (storage)
        {
            storage.Put(key, value);
            return true;
        }

        return false;
    }
    
    public bool PutTaskInfo(StorageContext storage, byte[] value)
    {
        return Put(storage, this.GetTaskInfoKey(), value);
    }
    
    #endregion
    
    #region Add
    public void Add(StorageContext storage, BigInteger taskID)
    {
        var tasks = GetTaskList(storage);
        tasks.Add<BigInteger>(taskID);
    }
    #endregion
    
    #region Remove
    public void Remove(StorageContext storage, BigInteger taskID)
    {
        var tasks = GetTaskList(storage);
        tasks.Remove<BigInteger>(taskID);
    }
    #endregion

    #region Delete

    public void DeleteInfo(StorageContext storage)
    {
        storage.Delete(this.GetTaskInfoKey());
    }
    
    public void DeleteRun(StorageContext storage)
    {
        storage.Delete(this.GetTaskRunKey());
    }
    #endregion
    
    #region Get
    public static byte[] GetTaskListKey()
    {
        return Encoding.ASCII.GetBytes($"{TaskListTag}");
    }

    private byte[] GetKeyForAddress(Address address)
    {
        return ByteArrayUtils.ConcatBytes(_prefix, address.ToByteArray());
    }
    
    public byte[] GetTaskRunKey()
    {
        return GetTaskKey(_taskID, TaskRunTag);
    }
    
    public byte[] GetTaskRun(StorageContext storage)
    {
        return storage.Get(GetTaskRunKey());
    }
    public byte[] GetTaskInfoKey()
    {
        return GetTaskKey(_taskID, TaskInfoTag);
    }

    public byte[] GetTaskInfo(StorageContext storage)
    {
        return storage.Get(GetTaskInfoKey());
    }
    
    public StorageList GetTaskList(StorageContext storage)
    {
        return new StorageList(GetTaskListKey(), storage);
    }
    #endregion
}
