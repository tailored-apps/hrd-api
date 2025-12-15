using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using HRD.Api;

namespace HRD.Api.Tests
{
    public class ProtocolTests
    {
        [Fact]
        public void BuildPacket_ConstructsCorrectPacket()
        {
            // Arrange
            byte[] apiHash = new byte[64]; // Dummy hash
            new Random().NextBytes(apiHash);
            
            string xml = "<test>data</test>";
            byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
            
            // Act
            byte[] packet = HRDApi.BuildPacket(apiHash, xml);
            
            // Assert
            // 1. Length Prefix (4 bytes)
            Assert.True(packet.Length > 4);
            int length = packet.Length - 4;
            
            byte[] lengthBytes = new byte[4];
            Array.Copy(packet, 0, lengthBytes, 0, 4);
            if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
            int lengthVal = BitConverter.ToInt32(lengthBytes, 0);
            
            Assert.Equal(length, lengthVal);
            
            // 2. Content (Hash + XML)
            // length should be 64 (hash) + xmlBytes.Length
            Assert.Equal(64 + xmlBytes.Length, length);
            
            // 3. Verify Hash
            byte[] packetHash = new byte[64];
            Array.Copy(packet, 4, packetHash, 0, 64);
            
            byte[] packetData = new byte[xmlBytes.Length];
            Array.Copy(packet, 4 + 64, packetData, 0, xmlBytes.Length);
            
            Assert.Equal(xmlBytes, packetData);
            
            // Recompute expected hash
            byte[] toHash = new byte[xmlBytes.Length + apiHash.Length];
            Buffer.BlockCopy(xmlBytes, 0, toHash, 0, xmlBytes.Length);
            Buffer.BlockCopy(apiHash, 0, toHash, xmlBytes.Length, apiHash.Length);
            
            byte[] expectedHash;
            using (var sha = SHA512.Create())
            {
                expectedHash = sha.ComputeHash(toHash);
            }
            
            Assert.Equal(expectedHash, packetHash);
        }
    }
}
