using System.Linq;
using System.Numerics;

namespace Phantasma.Neo.Cryptography.ECC
{
    public enum ECDsaCurve
    {
        Secp256r1,
        Secp256k1,
    }

    public static class ECCCurveExtensions
    {
        public static ECCurve GetCurve(this ECDsaCurve curve)
        {
            switch (curve)
            {
                case ECDsaCurve.Secp256k1: return ECCurve.Secp256k1;
                case ECDsaCurve.Secp256r1: return ECCurve.Secp256r1;
                default: return null;
            }
        }
    }

    public class ECCurve
    {
        internal readonly BigInteger Q;
        internal readonly ECFieldElement A;
        internal readonly ECFieldElement B;
        internal readonly BigInteger N;
        public readonly ECPoint Infinity;
        public readonly ECPoint G;

        private ECCurve(BigInteger Q, BigInteger A, BigInteger B, BigInteger N, byte[] G)
        {
            this.Q = Q;
            this.A = new ECFieldElement(A, this);
            this.B = new ECFieldElement(B, this);
            this.N = N;
            this.Infinity = new ECPoint(null, null, this);
            this.G = ECPoint.DecodePoint(G, this);
        }

        public static readonly ECCurve Secp256k1 = new ECCurve
        (
            BigInteger.FromSignedArray(Base16.Decode("00FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFC2F").Reverse().ToArray()),
            BigInteger.Zero,
            7,
            BigInteger.FromSignedArray(Base16.Decode("00FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141").Reverse().ToArray()),
            Base16.Decode("0479BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798" + "483ADA7726A3C4655DA4FBFC0E1108A8FD17B448A68554199C47D08FFB10D4B8")
        );

        // AKA NIST P-256
        public static readonly ECCurve Secp256r1 = new ECCurve
        (
            BigInteger.FromSignedArray(Base16.Decode("00FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF").Reverse().ToArray()),
            BigInteger.FromSignedArray(Base16.Decode("00FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFC").Reverse().ToArray()),
            BigInteger.FromSignedArray(Base16.Decode("005AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B").Reverse().ToArray()),
            BigInteger.FromSignedArray(Base16.Decode("00FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551").Reverse().ToArray()),
            Base16.Decode("046B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296" + "4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5")
        );
    }
}
