using System.Collections.Generic;
using System.Linq;
using Phantasma.Core;

namespace Phantasma.Core;

public class TransactionResult
{
    public Hash Hash{ get; set; }

    public uint Code { get; set; }

    public VMObject Result { get; set; }

    public string Log { get; set; }

    public string Info { get; set; }

    public long Gas { get; set; }

    public long GasUsed { get; set; }

    public Event[] Events { get; set; }

    public string Codespace { get; set; }


    public TransactionResult() {}
    public TransactionResult(uint code, VMObject result, string log, string info, long gas, long gasUsed, IEnumerable<Event> events,
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
