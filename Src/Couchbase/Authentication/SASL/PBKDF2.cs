using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Authentication.SASL
{
    //Generic PBKDF2 Class that can use any HMAC algorithm derived from the
    // System.Security.Cryptography.HMAC abstract class

    // PER SPEC RFC2898 with help from user Dodgyrabbit on StackExchange
    // http://stackoverflow.com/questions/1046599/pbkdf2-implementation-in-c-sharp-with-rfc2898derivebytes

    // the use of default values for parameters in the functions puts this at .NET 4
    // if you remove those defaults and create the required constructors, you should be able to drop to .NET 2

    // USE AT YOUR OWN RISK!  I HAVE TESTED THIS AGAINST PUBLIC TEST VECTORS, BUT YOU SHOULD
    // HAVE YOUR CODE PEER-REVIEWED AND SHOULD FOLLOW BEST PRACTICES WHEN USING CRYPTO-ANYTHING!
    // NO WARRANTY IMPLIED OR EXPRESSED, YOU ARE ON YOUR OWN!

    // PUBLIC DOMAIN!  NO COPYRIGHT INTENDED OR RESERVED!

    //constrain T to be any class that derives from HMAC, and that exposes a new() constructor
    public class PBKDF2<T> : DeriveBytes where T : HMAC, new()
    {
        //Internal variables and public properties
        private int _blockSize = -1; // the byte width of the output of the HMAC algorithm
        byte[] _P = null;
        int _C = 0;
        private T _hmac;

        byte[] _S = null;
        // if you called the initializer/constructor specifying a salt size,
        // you will need this property to GET the salt after it was created from the crypto rng!
        // GET THIS BEFORE CALLING GETBYTES()!  OBJECT WILL BE RESET AFTER GETBYTES() AND
        // SALT WILL BE LOST!!
        public byte[] Salt
        {
            get { return (byte[]) _S.Clone(); }
        }

        // Constructors
        public PBKDF2(string Password, byte[] Salt, int IterationCount = 1000)
        {
            Initialize(Password, Salt, IterationCount);
        }

        public PBKDF2(byte[] Password, byte[] Salt, int IterationCount = 1000)
        {
            Initialize(Password, Salt, IterationCount);
        }

        public PBKDF2(string Password, int SizeOfSaltInBytes, int IterationCount = 1000)
        {
            Initialize(Password, SizeOfSaltInBytes, IterationCount);
        }

        public PBKDF2(byte[] Password, int SizeOfSaltInBytes, int IterationCount = 1000)
        {
            Initialize(Password, SizeOfSaltInBytes, IterationCount);
        }

        //All Construtors call the corresponding Initialize methods
        public void Initialize(string Password, byte[] Salt, int IterationCount = 1000)
        {
            if (string.IsNullOrWhiteSpace(Password))
                throw new ArgumentException("Password must contain meaningful characters and not be null.", "Password");
            if (IterationCount < 1)
                throw new ArgumentOutOfRangeException("IterationCount");
            Initialize(new UTF8Encoding(false).GetBytes(Password), Salt, IterationCount);
        }

        public void Initialize(byte[] Password, byte[] Salt, int IterationCount = 1000)
        {
            //all Constructors/Initializers eventually lead to this one which does all the "important" work
            if (Password == null || Password.Length == 0)
                throw new ArgumentException("Password cannot be null or empty.", "Password");
            if (Salt == null)
                Salt = new byte[0];
            if (IterationCount < 1)
                throw new ArgumentOutOfRangeException("IterationCount");
            _P = (byte[]) Password.Clone();
            _S = (byte[]) Salt.Clone();
            _C = IterationCount;
            //determine _blockSize
            _hmac = new T();
            _hmac.Key = new byte[] {0};
            byte[] test = _hmac.ComputeHash(new byte[] {0});
            _blockSize = test.Length;
        }

        public void Initialize(string Password, int SizeOfSaltInBytes, int IterationCount = 1000)
        {
            if (string.IsNullOrWhiteSpace(Password))
                throw new ArgumentException("Password must contain meaningful characters and not be null.", "Password");
            if (IterationCount < 1)
                throw new ArgumentOutOfRangeException("IterationCount");
            Initialize(new UTF8Encoding(false).GetBytes(Password), SizeOfSaltInBytes, IterationCount);
        }

        public void Initialize(byte[] Password, int SizeOfSaltInBytes, int IterationCount = 1000)
        {
            if (Password == null || Password.Length == 0)
                throw new ArgumentException("Password cannot be null or empty.", "Password");
            if (SizeOfSaltInBytes < 0)
                throw new ArgumentOutOfRangeException("SizeOfSaltInBytes");
            if (IterationCount < 1)
                throw new ArgumentOutOfRangeException("IterationCount");
            // You didn't specify a salt, so I'm going to create one for you of the specific byte length
            byte[] data = new byte[SizeOfSaltInBytes];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(data);
            // and then finish initializing...
            // Get the salt from the Salt parameter BEFORE calling GetBytes()!!!!!!!!!!!
            Initialize(Password, data, IterationCount);
        }

        ~PBKDF2()
        {
            //*DOOT* clean up in aisle 5! *KEKERKCRACKLE*
            this.Reset();
        }

        // required by the Derive Bytes class/interface
        // this is where you request your output bytes after Initialize
        // state of class Reset after use!
        public override byte[] GetBytes(int ByteCount)
        {
            if (_S == null || _P == null)
                throw new InvalidOperationException("Object not Initialized!");
            if (ByteCount < 1) // || ByteCount > uint.MaxValue * blockSize)
                throw new ArgumentOutOfRangeException("ByteCount");

            int totalBlocks = (int) Math.Ceiling((decimal) ByteCount/_blockSize);
            int partialBlock = (int) (ByteCount%_blockSize);
            byte[] result = new byte[ByteCount];
            byte[] buffer = null;
            // I'm using TT here instead of T from the spec because I don't want to confuse it with
            // the generic object T
            for (int TT = 1; TT <= totalBlocks; TT++)
            {
                // run the F function with the _C number of iterations for block number TT
                buffer = _F((uint) TT);
                //IF we're not at the last block requested
                //OR the last block requested is whole (not partial)
                //  then take everything from the result of F for this block number TT
                //ELSE only take the needed bytes from F
                if (TT != totalBlocks || (TT == totalBlocks && partialBlock == 0))
                    Buffer.BlockCopy(buffer, 0, result, _blockSize*(TT - 1), _blockSize);
                else
                    Buffer.BlockCopy(buffer, 0, result, _blockSize*(TT - 1), partialBlock);
            }
            this.Reset(); // force cleanup after every use!  Cannot be reused!
            return result;
        }

        // required by the Derive Bytes class/interface
        public override void Reset()
        {
            _C = 0;
            _P.Initialize(); // the compiler might optimize this line out! :(
            _P = null;
            _S.Initialize(); // the compiler might optimize this line out! :(
            _S = null;
            if (_hmac != null)
                _hmac.Clear();
            _blockSize = -1;
        }

        // the core function of the PBKDF which does all the iterations
        // per the spec section 5.2 step 3
        private byte[] _F(uint I)
        {
            //NOTE: SPEC IS MISLEADING!!!
            //THE HMAC FUNCTIONS ARE KEYED BY THE PASSWORD! NEVER THE SALT!
            byte[] bufferU = null;
            byte[] bufferOut = null;
            byte[] _int = PBKDF2<T>.IntToBytes(I);
            _hmac = new T();
            _hmac.Key = (_P); // KEY BY THE PASSWORD!
            _hmac.TransformBlock(_S, 0, _S.Length, _S, 0);
            _hmac.TransformFinalBlock(_int, 0, _int.Length);
            bufferU = _hmac.Hash;
            bufferOut = (byte[]) bufferU.Clone();
            for (int c = 1; c < _C; c++)
            {
                _hmac.Initialize();
                _hmac.Key = _P; // KEY BY THE PASSWORD!
                bufferU = _hmac.ComputeHash(bufferU);
                _Xor(ref bufferOut, bufferU);
            }
            return bufferOut;
        }

        // XOR one array of bytes into another (which is passed by reference)
        // this is the equiv of data ^= newData;
        private void _Xor(ref byte[] data, byte[] newData)
        {
            for (int i = data.GetLowerBound(0); i <= data.GetUpperBound(0); i++)
                data[i] ^= newData[i];
        }

        // convert an unsigned int into an array of bytes BIG ENDIEN
        // per the spec section 5.2 step 3
        static internal byte[] IntToBytes(uint i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            if (!BitConverter.IsLittleEndian)
            {
                return bytes;
            }
            else
            {
                Array.Reverse(bytes);
                return bytes;
            }
        }
    }
}
