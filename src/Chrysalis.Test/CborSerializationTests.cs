using System.Reflection;
using Chrysalis.Cardano.Models;
using Chrysalis.Cardano.Models.Cbor;
using Chrysalis.Cardano.Models.Coinecta;
using Chrysalis.Cardano.Models.Coinecta.Vesting;
using Chrysalis.Cardano.Models.Core;
using Chrysalis.Cardano.Models.Core.Block;
using Chrysalis.Cardano.Models.Plutus;
using Chrysalis.Cardano.Models.Sundae;
using Chrysalis.Cbor;
using Xunit;

namespace Chrysalis.Test;

public class CborSerializerTests
{
    [Theory]
    [InlineData("1834", typeof(CborInt))] // Example hex for CBOR int 52
    [InlineData("4101", typeof(CborBytes))] // Example hex for CBOR bytes {0x01)]
    [InlineData("1a000f4240", typeof(CborUlong))] // Example hex for CBOR ulong 1_000_000
    [InlineData("1a000f4240", typeof(PosixTime))] // Example hex for CBOR ulong 1_000_000
    [InlineData("43414243", typeof(CborBytes))] // Example hex for CBOR bytes of `ABC` string
    [InlineData("a141614541696b656e", typeof(CborMap<CborBytes, CborBytes>))] // {h'61': h'41696b656e'}
    [InlineData("a1450001020304a1450001020304182a", typeof(MultiAsset))] // {h'61': h'41696b656e'}
    [InlineData("9f0102030405ff", typeof(CborIndefiniteList<CborInt>))] // [_ 1, 2, 3, 4, 5]
    [InlineData("850102030405", typeof(CborDefiniteList<CborInt>))] // [1, 2, 3, 4, 5]
    [InlineData("9f824401020304182a824405060708182bff", typeof(CborIndefiniteList<TransactionInput>))] // [_ [h'01020304', 42_0], [h'05060708', 43_0]]
    [InlineData("d8799f182aff", typeof(Option<CborInt>))] // Serialized CBOR for Option::Some(42):
    [InlineData("d87a80", typeof(Option<CborInt>))] // Serialized CBOR for Option::None:
    [InlineData("d8799f4180ff", typeof(Signature))] // Serialized CBOR for Signature:
    [InlineData("d87c9f029fd8799f446b657931ffd8799f446b657932ffd8799f446b657933ffffff", typeof(AtLeast))] // Serialized CBOR for AtLeast Multisig:
    [InlineData("d8799fd8799f581ceca3dfbde8ccb8408cefacda690e34aa9353af93fc02e75d8ba42f1bff58202325f3c999b17d4a6399bf6c02e1ff7615c13a73ecafae7fe813b9757f27ef2600ff", typeof(Treasury))] // Serialized CBOR for Signature:
    [InlineData("8201d818587ed8799fd8799fd87a9f581c1eae96baf29e27682ea3f815aba361a0c6059d45e4bfbe95bbd2f44affffd8799f4040ffd8799f581caf65a4734e8a22f43128913567566d2dde30d3b3298306d6317570f64e0014df104d494e20496e7465726eff1a2a2597de1a009896801b0000000ba43b740018641864d87a80d87980ff", typeof(DatumOption))] // Serialized CBOR for Inline Datum:
    [InlineData("d81842ffff", typeof(CborEncodedValue))] // Serialized CBOR for CIP68:
    [InlineData("a300583911ea07b733d932129c378af627436e7cbc2ef0bf96e0036bb51b3bde6b52563c5410bff6a0d43ccebb7c37e1f69f5eb260552521adff33b9c201821a00dd40a0a2581caf65a4734e8a22f43128913567566d2dde30d3b3298306d6317570f6a14e0014df104d494e20496e7465726e1b0000000ba43b7400581cf5808c2c990d86da54bfc97d89cee6efa20cd8461616359478d96b4ca2434d5350015820e08460587b08cca542bd2856b8d5e1d23bf3f63f9916fb81f6d95fda0910bf691b7fffffffd5da682b028201d818587ed8799fd8799fd87a9f581c1eae96baf29e27682ea3f815aba361a0c6059d45e4bfbe95bbd2f44affffd8799f4040ffd8799f581caf65a4734e8a22f43128913567566d2dde30d3b3298306d6317570f64e0014df104d494e20496e7465726eff1a2a2597de1a009896801b0000000ba43b740018641864d87a80d87980ff", typeof(TransactionOutput))] // Serialized CBOR for TransactionOutput:
    [InlineData("D8799F9FD87B9F005820567463495E4DC4FB67268D9A6E92836A68A18A317D2F0CA6CC6D695EE7733889582023B4DA2A35E86C585A6F5FCCFF3B53F7660D73536C79FF486BCAB719B518C58FFFFFD8799FD8799F581CA7E1D2E57B1F9AA851B08C8934A315FFD97397FA997BB3851C626D3BFFA0A140A1401A05F5E10041004100FFFF", typeof(TreasuryRedeemer))] // Serialized CBOR for TreasuryRedeemer:
    [InlineData("820800", typeof(ProtocolVersion))] // Serialized CBOR for ProtocolVersion(Conway)
    [InlineData("8458205c04fb3111739a79e318135ef5bbfcba008533f2315ec5525210856024f6917f151903f65840d5341b64c77472028d78fabbc43a3873f3ae8f7e2daf6dc9e475024acbc179d42cb014ae4acd79dc2cd28c6186f4b1c07edaeecd3c48ec1cdc30e969b6ef3a03", typeof(OperationalCert))] // Serialized CBOR for OperationalCert(Conway)
    [InlineData("825840bb060ee2d34b4e23c5d402ba181330ecf32777cdf745789aea9afe94c1b0df474fd2fb60e6965b247b338ebcf337c383b57731c016e71d93fd5e5f8dc8665915585060941c4ac096a3a5e25c16c910bb85393012a02b14623470130c27cf6d27ea37ed5b03c8051ec2f237ab4fd5f43aff2289721d573e8e7de7515fda4143f4dc611b5f399467c726e3375df00617cb3b05", typeof(VrfCert))] // Serialized CBOR for VrfCert(Conway)
    [InlineData("8a1a00a4cdc11a07fd9ced5820f900395a8b763e7fb2630d3b4b4f8925e225205e5a4544275778bd8c5eb4f14c5820b4a64416f171e59d66e0d1890e9146211c2fb4e70930ea782f0f4983decb285e5820399f9f34c7947c86992b9cce3e6a58343d3d770de8b1525b2dea88893905efbf825840bb060ee2d34b4e23c5d402ba181330ecf32777cdf745789aea9afe94c1b0df474fd2fb60e6965b247b338ebcf337c383b57731c016e71d93fd5e5f8dc8665915585060941c4ac096a3a5e25c16c910bb85393012a02b14623470130c27cf6d27ea37ed5b03c8051ec2f237ab4fd5f43aff2289721d573e8e7de7515fda4143f4dc611b5f399467c726e3375df00617cb3b051a000150bd5820556a0b2a892073e5c0c598d3a04737d827c3708d2c9cb04c7142d80ddd1385908458205c04fb3111739a79e318135ef5bbfcba008533f2315ec5525210856024f6917f151903f65840d5341b64c77472028d78fabbc43a3873f3ae8f7e2daf6dc9e475024acbc179d42cb014ae4acd79dc2cd28c6186f4b1c07edaeecd3c48ec1cdc30e969b6ef3a03820901", typeof(BlockHeaderBody))] // Serialized CBOR for HeaderBody(Conway)
    [InlineData("8a1a00a076f71a07a51cec58203228c42f0af94c7576946942d7494634b11c4562e5d5257730a5e632dfd44bf358209ab6d0f36afdf63b527ab257f5a3f6625f2844c6cd83eecce3a1d8811cadb9b958208e4715edc8ef2d6a8c1860b11675fe0afa186eebf2cfb62aaf4c3526b8c5bd4882584066a21b2c4f9aa32fb5b499bab11891f2abce92a6f51f8ff5ed5dd4a6eeee750e23bd1505ea910637cad190766f90998fa7865a8f776ff0a3707f374abaddfe45585044bee1c7492d7d08066de3fd7e76b8e2ec48a2887cfc4c5152083edd3850f3e4969f33700163b1139ddaffb3a6cfd29e995bcbde1e1b063bb288f660e4c0ba6653233ab741762fe5b1a5368ed0fbb30819c5ca5820ca1158d016cd2cb338ce3d9f04e6f9f6314cd4b2054b5391907808f5f262c7dc845820878946c26f0fe05879a5591d77fa7b26ff6a7fa303c2ef9fd81bfce791f34ce40c1903bf58409fd5ff67c18649c50f3169e6680b33b011562a952b6fe97918c95c8604141858b8e771315c9c87d4a81843b1d62ba60e52abb9df46ad135c621aee9e5b80cc02820800", typeof(BlockHeaderBody))] // Serialized CBOR for HeaderBody(Babbage)
    [InlineData("8f1a006b0f171a035877e8582059aab61986b323527011efcc941a350b36d6d59edc21fe1600e77be8a354c7655820f492625f14992782625aac4177b76130749c888d0df6adf2da26c7086f74437d5820ab290b1f62e86b6079c179b462a4de11b2028148124be71445e083b62f535b3782584020b88ead1afcaf8880a61a1ba83793fac9a358661888fe34376709b1e18ffbd74dd9d977b2aa51b926a3755da2d0125bdf2f5018de4b47c3131ce8d4db900ee65850f10b18ce2d63c8864b182efdd221867b5c90fa02627bf594e064490a3f9f1a068cbf285446462b931c797166e7061db8189b46b250693e64236c5fc98e115f3dbc423d108fbd5cc44cd16fa917ddce0a8258400004d68febdfe65d2f45e343af88b41746ce3087e16180c0e574e1abade72f2d969f69b61024b83b682b59a773cd04f6989b2b14805371122e70fca09284ecb558504dce6f572748203bb1ee6c8162bc644d9476d56a0a97632877a014b669e48234a0a70b7c1d88b822042ff6419fb0c73521c79a6b508d9c46001e802c5bdb51b6b18430461ae518314c54c2a5d1e81b0d19b38d5820477740dfd9ad3bb33ab534ed6dc1e06e455a3397fc9adabb972220a326c0b58b5820da24cac0e7a88a4bda8a8b5f44e59c2ace45c75fac0a797b4acd352a007a6753061901a45840c42104b2b016433ade455b25aa4e81073152dfd825a7b65922c573a01f07a0f987f70243968e5348a58772458a1031144e1c1b28e7748136e87be18e29e87f0d0600", typeof(BlockHeaderBody))] // Serialized CBOR for HeaderBody(Alonzo)
    [InlineData("828f1a006b0f171a035877e8582059aab61986b323527011efcc941a350b36d6d59edc21fe1600e77be8a354c7655820f492625f14992782625aac4177b76130749c888d0df6adf2da26c7086f74437d5820ab290b1f62e86b6079c179b462a4de11b2028148124be71445e083b62f535b3782584020b88ead1afcaf8880a61a1ba83793fac9a358661888fe34376709b1e18ffbd74dd9d977b2aa51b926a3755da2d0125bdf2f5018de4b47c3131ce8d4db900ee65850f10b18ce2d63c8864b182efdd221867b5c90fa02627bf594e064490a3f9f1a068cbf285446462b931c797166e7061db8189b46b250693e64236c5fc98e115f3dbc423d108fbd5cc44cd16fa917ddce0a8258400004d68febdfe65d2f45e343af88b41746ce3087e16180c0e574e1abade72f2d969f69b61024b83b682b59a773cd04f6989b2b14805371122e70fca09284ecb558504dce6f572748203bb1ee6c8162bc644d9476d56a0a97632877a014b669e48234a0a70b7c1d88b822042ff6419fb0c73521c79a6b508d9c46001e802c5bdb51b6b18430461ae518314c54c2a5d1e81b0d19b38d5820477740dfd9ad3bb33ab534ed6dc1e06e455a3397fc9adabb972220a326c0b58b5820da24cac0e7a88a4bda8a8b5f44e59c2ace45c75fac0a797b4acd352a007a6753061901a45840c42104b2b016433ade455b25aa4e81073152dfd825a7b65922c573a01f07a0f987f70243968e5348a58772458a1031144e1c1b28e7748136e87be18e29e87f0d06005901c0ac1591f54da90ca03498d4667023f30fd09afa207b80f3f0866c730e91558ee4b10ede3810d69b842d06dc679522ad98722530bf6d8452aa2b8e6f1f5d1e9304784659cec31ebf7f29e6deff79d5d6a2842c24cd20f5ce02f80e08678daa907087b068c812eb5523afa0438134e73b2382478f93fe82d0d2b3ea7a9c1a3606f34ca3c89f0d84b280d5f716661cd93254b14c18d99d51f6a2ebd175210b5524ceb8ad3ec0ec9ca2564b168e7e00ec96e0271c47328468950edd89f4bca390acebd53413c9344a0b2a8fcb4ec668949450ad6381d2af7bbac5d024f0251b114b3732072c2851b82fbc7f5f8ce0640fd14c29dc2d591736a17c90036f7efbf8ed00239a028ee0890286615c5bbe88c6ead7d5d821d34b111133bb9806c8bb8b73d065959a427dfd5fcea80a0bbda6a07c2ed870ac8c728a57751aef30902ea8cda5961015fe7f843f892a1ae978cdd55b57e14497cc48d540f42449e803637727403dad6c188b64a61e94ab5bfed8c39360f18347aaeae473a069a6c479db3fbe190a3bb236eea6e9b163c628effafc287b081041f94df6a91a62f50ae6740cb980da7a9f5e2080a98b4a6c73de537b2a70ad50df7aea318d34ef6f2e860e511f40", typeof(BlockHeader))] // Serialized CBOR for Header(Alonzo)
    [InlineData("828a1a00a076f71a07a51cec58203228c42f0af94c7576946942d7494634b11c4562e5d5257730a5e632dfd44bf358209ab6d0f36afdf63b527ab257f5a3f6625f2844c6cd83eecce3a1d8811cadb9b958208e4715edc8ef2d6a8c1860b11675fe0afa186eebf2cfb62aaf4c3526b8c5bd4882584066a21b2c4f9aa32fb5b499bab11891f2abce92a6f51f8ff5ed5dd4a6eeee750e23bd1505ea910637cad190766f90998fa7865a8f776ff0a3707f374abaddfe45585044bee1c7492d7d08066de3fd7e76b8e2ec48a2887cfc4c5152083edd3850f3e4969f33700163b1139ddaffb3a6cfd29e995bcbde1e1b063bb288f660e4c0ba6653233ab741762fe5b1a5368ed0fbb30819c5ca5820ca1158d016cd2cb338ce3d9f04e6f9f6314cd4b2054b5391907808f5f262c7dc845820878946c26f0fe05879a5591d77fa7b26ff6a7fa303c2ef9fd81bfce791f34ce40c1903bf58409fd5ff67c18649c50f3169e6680b33b011562a952b6fe97918c95c8604141858b8e771315c9c87d4a81843b1d62ba60e52abb9df46ad135c621aee9e5b80cc028208005901c07475c5276afbfd46e7eb54515467eb3244c40c915b90378771ae235d6efc4a3ee3f6f0ab63dae2e4e3733800d61ca44b48601149f1eb47e6771326d9b2111c04f4ad4d5f9a7044b8aabedb260b86ef0472149a110b351f2a13dd93f602e2980aa06f037f755e9093d96130e4a1533fb6b20a1356185f53f39548ef569773970348f2b0638a72fe6d06b75abc29e408216c229cb4fa8956ffd36eb7c79d741274f28fee2ecda595d62e164c3081f077e590f58b1ae247c05f574c3c0b6e9b7588c19260c4ccdd9a8382896b17fbca50014ed2a72879b30c500821fdd45cfa8f1ba7183da404d63dea8261a193e7cd499c571119a73c0aaa1bffba9410a7878655b3b13136e197a5e56e21af0f9d41c0fb6a98bdbf77011128b49def09d9c351339cb3d44de4945a922fac512b87136c2b3cb3e5e57ea3474f604f8b89b722124a5ffbc6b3c85d9eda86403043912e1a2b5e500805a838da39caf4818d310e18be8792b56f37072223bff98fb9829e57676526768c2db4e0fb8fa8340b2cdcde0178614d5a25dcba2b31ef71f4b5d80b2581121c8044d98ce2b1b26ffa9f350b5184fd6dd1c24833972db817e2b8c34340933c652ff5e59acf906a56b5402a831f", typeof(BlockHeader))] // Serialized CBOR for Header(Babbage)
    [InlineData("828a1a00a4cdc11a07fd9ced5820f900395a8b763e7fb2630d3b4b4f8925e225205e5a4544275778bd8c5eb4f14c5820b4a64416f171e59d66e0d1890e9146211c2fb4e70930ea782f0f4983decb285e5820399f9f34c7947c86992b9cce3e6a58343d3d770de8b1525b2dea88893905efbf825840bb060ee2d34b4e23c5d402ba181330ecf32777cdf745789aea9afe94c1b0df474fd2fb60e6965b247b338ebcf337c383b57731c016e71d93fd5e5f8dc8665915585060941c4ac096a3a5e25c16c910bb85393012a02b14623470130c27cf6d27ea37ed5b03c8051ec2f237ab4fd5f43aff2289721d573e8e7de7515fda4143f4dc611b5f399467c726e3375df00617cb3b051a000150bd5820556a0b2a892073e5c0c598d3a04737d827c3708d2c9cb04c7142d80ddd1385908458205c04fb3111739a79e318135ef5bbfcba008533f2315ec5525210856024f6917f151903f65840d5341b64c77472028d78fabbc43a3873f3ae8f7e2daf6dc9e475024acbc179d42cb014ae4acd79dc2cd28c6186f4b1c07edaeecd3c48ec1cdc30e969b6ef3a038209015901c0e4dcaeeff4c6bbd5e47fea3ac20aee40574b450903a811a0761297aef0892111b54481e88c363ca0025501c58500efddac809cc3f10ac6289e9929d8560c230483f928a0a5016d9ae0d3ba43233983bf32014df16c02bc66a917b85d98e12bad15df95de5ffcd62e4fbdb8c50377fa6b8da5c43a27ce95923bc1cbb4faa72c9e9630926675ec4c92c5f1de8212b1b904dabc750222edce42dcaf58d236b2783f719c908d6841f177e82f2e76ca3ca0f96cbe936d24a658d26f23975572c01cb7681a6017c8d85e34b5ac343c6db51ae28d0cdf9f3eea3ef5e9b86a6c38aaab7bbc0e15b84b73ae035f0b75af9172facb7d469c743ce38f8a2d8009a1e910e1832e44037c03627e87993f83d7dc570b76f24123b93e0413c95af4f40df331b333af7f346c4067c2bb9ed83cd071a4e94048ee2b3395918bfcda4e1ecc035b4eb134e6ecbf337a64d16f1b0121918aa4347897ba30e3ec74aa8e502bcdd70d054c340648d2768303b7f095edaf28358b9f97b0093aa6c0bc76525664a0b9d0c78d872b8f7c7b840c0e3292b7c910655da1c0bffeaae80298593267c979c196cffecaff571ba6248acc7c4c338b3c7a93584d04a47c74c2c5beda2e6f4aa26d4f39", typeof(BlockHeader))] // Serialized CBOR for Header(Conway)
    public void SerializeAndDeserializePrimitives(string cborHex, Type type)
    {
        // Arrange
        byte[] cborBytes = Convert.FromHexString(cborHex);

        // Act
        MethodInfo? deserializeMethod = typeof(CborSerializer).GetMethod(nameof(CborSerializer.Deserialize));
        Assert.NotNull(deserializeMethod);

        MethodInfo? genericDeserializeMethod = deserializeMethod?.MakeGenericMethod(type);
        Assert.NotNull(genericDeserializeMethod);

        object? cborObject = genericDeserializeMethod?.Invoke(null, [cborBytes]);
        Assert.NotNull(cborObject);

        byte[] serializedBytes = CborSerializer.Serialize((ICbor)cborObject);
        string serializedHex = Convert.ToHexString(serializedBytes).ToLowerInvariant();

        // Assert
        Assert.Equal(cborHex.ToLowerInvariant(), serializedHex);
    }
}