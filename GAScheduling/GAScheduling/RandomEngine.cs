using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GAScheduling
{
    class RandomEngine                                         //由于默认的随机数生成器以当前时间（ms）为种子，两次生成间隔过短会产生同样的随机序列。
    {                                                          //我们使用系统的加密服务提供的随机数生成器为默认随机数生成器初始化
        private static RNGCryptoServiceProvider mSeedGen;      //该生成器使用更为严密的算法，生成的随机数更为可靠

        private static Random mNumberGen;

        static RandomEngine()
        {
            mSeedGen = new RNGCryptoServiceProvider();
            NewSeed();
        }
        
        public static void NewSeed()
        {
            byte[] bytes = new byte[4];
            mSeedGen.GetBytes(bytes);
            mNumberGen = new Random(BitConverter.ToInt32(bytes, 0));
        }

        public static int Next(int maxValue)
        {
            return mNumberGen.Next(maxValue);
        }

        public static double NextDouble()
        {
            return mNumberGen.NextDouble();
        }
    }
}
