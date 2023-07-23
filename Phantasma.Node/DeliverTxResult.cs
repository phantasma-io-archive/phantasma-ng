using System.Collections.Generic;
using System.Linq;
using Phantasma.Core.Cryptography;
using Phantasma.Core.Cryptography.Structs;
using Phantasma.Core.Domain;
using Phantasma.Core.Domain.Structs;

namespace Phantasma.Node;
public class DeliverTxResult
{
    public Hash Hash { get; set; }
    public uint Code { get; set; }
    
    public byte[] Result { get; set; }
    
    public string Log { get; set; }
    
    public string Info { get; set; }
    
    public long Gas { get; set; }
    
    public long GasUsed { get; set; }
    
    public Event[] Events { get; set; }
    
    public string Codespace { get; set; }

    
    public DeliverTxResult() {}
    public DeliverTxResult(uint code, byte[] result, string log, string info, long gas, long gasUsed, IEnumerable<Event> events,
        string codespace)
    {
        this.Code = code;
        this.Result = result;
        this.Log = log;
        this.Info = info;
        this.Gas = gas;
        this.GasUsed = gasUsed;
        this.Events = events.ToArray();
        this.Codespace = codespace;
    }
}
