using FluentAssertions;
using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace Anime4KSharp.Test
{
    public class UnitTest1
    {
        private const string outPath = "out.png";

        [Fact]
        public void Test_It_Works()
        {
            string[] md5s = new string[] { "CSR0kYNM6HDFRDeUiLNjmg==", "IvkTD7CjkHVb2AzzIH+nZA==", "A6u6ttIpSFNf52VFq2BZCA==" };// "Zjr5i3YRCO1jt5K9lBhFdQ ==";//different output when running the app!?!?!?!??!

            Program.Main(new string[] { @"..\..\..\..\images\Rand0mZ_King_Downscaled_256px.png", outPath });

            string outHash = Convert.ToBase64String(MD5.Create().ComputeHash(File.OpenRead(outPath)));

            md5s.Should().Contain(outHash);
        }

    }
}
