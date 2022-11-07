using Google.Protobuf;
using Tendermint.Abci;
using Phantasma.Business.Blockchain;

namespace Tendermint
{
    public static class ResponseHelper
    {
        public static class Check
        {
            public static ResponseCheckTx Create(CodeType codeType, string log = "") => new ResponseCheckTx()
            {
                Code = (uint) codeType,
                Log = log
            };

            public static ResponseCheckTx Ok() => new ResponseCheckTx() { Code = (uint) CodeType.Ok };

            //public static ResponseCheckTx Unauthorized() => new ResponseCheckTx()
            //{
            //    Code = (uint) CodeType.Unauthorized,
            //    Log = "Unauthorized request"
            //};

            //public static ResponseCheckTx NoPayload() => new ResponseCheckTx()
            //{
            //    Code = (uint) CodeType.NoPayload,
            //    Log = "No payload received."
            //};
        }

        public static class Deliver
        {
            public static ResponseDeliverTx Ok() => new ResponseDeliverTx() { Code = (uint) CodeType.Ok };

            public static ResponseDeliverTx Create(CodeType codeType, string log = "") => new ResponseDeliverTx()
            {
                Code = (uint) codeType,
                Log = log
            };

            //public static ResponseDeliverTx Unauthorized() => new ResponseDeliverTx()
            //{
            //    Code = (uint) CodeType.Unauthorized,
            //    Log = "Unauthorized request"
            //};

            //public static ResponseDeliverTx NoPayload() => new ResponseDeliverTx()
            //{
            //    Code = (uint) CodeType.NoPayload,
            //    Log = "No payload received."
            //};
        }

        public static class Query
        {
            public static ResponseQuery Ok(ByteString data) => new ResponseQuery() { Code = (uint) CodeType.Ok, Value = data };
        }
    }
}
