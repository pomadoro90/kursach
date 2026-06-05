using NUnit.Framework;

namespace FlareSystem.Tests
{
    public class FlareCsvLoaderTests
    {
        [Test]
        public void LoadRecords_ReadsSemicolonCsv()
        {
            var records = new FlareCsvLoader().LoadRecords("variant_3_15.csv");

            Assert.That(records, Is.Not.Null);
            Assert.That(records.Count, Is.EqualTo(100));
            Assert.That(records[0].N, Is.EqualTo(1));
            Assert.That(records[0].P_flare, Is.GreaterThan(0f));
        }
    }
}
