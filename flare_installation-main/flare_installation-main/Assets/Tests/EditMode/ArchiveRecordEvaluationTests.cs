using NUnit.Framework;

namespace FlareSystem.Tests
{
    public class ArchiveRecordEvaluationTests
    {
        [Test]
        public void EvaluateRecord_HlopokFlagProducesAlarm()
        {
            var record = new FlareDataRecord
            {
                N = 3,
                P_flare = 0.012f,
                Q_flare = 3f,
                P_purge = 0.35f,
                Q_purge = 30f,
                T_flame = 950f,
                Steam_Q = 90f,
                otriv = 0,
                hlopok = 1
            };

            FlareSystemState state = FlareConstants.EvaluateRecord(record, FlareMode.Archive, 0.1f);

            Assert.That(state.IsAlarm, Is.True);
            Assert.That(state.Recommendation, Does.Contain("хлопок"));
        }

        [Test]
        public void EvaluateRecord_PFlareAboveHlopokThresholdProducesAlarm()
        {
            var record = new FlareDataRecord
            {
                N = 4,
                P_flare = 0.023f,
                Q_flare = 3f,
                P_purge = 0.35f,
                Q_purge = 30f,
                T_flame = 950f,
                Steam_Q = 90f,
                otriv = 0,
                hlopok = 0
            };

            FlareSystemState state = FlareConstants.EvaluateRecord(record, FlareMode.Archive, 0.1f);

            Assert.That(state.IsAlarm, Is.True);
            Assert.That(state.Recommendation, Does.Contain("Снизить давление"));
        }

        [Test]
        public void EvaluateRecord_OtrivFlagProducesAlarm()
        {
            var record = new FlareDataRecord
            {
                N = 5,
                P_flare = 0.01f,
                Q_flare = 3f,
                P_purge = 0.35f,
                Q_purge = 30f,
                T_flame = 950f,
                Steam_Q = 90f,
                otriv = 1,
                hlopok = 0
            };

            FlareSystemState state = FlareConstants.EvaluateRecord(record, FlareMode.Archive, 0.1f);

            Assert.That(state.IsAlarm, Is.True);
            Assert.That(state.Recommendation, Does.Contain("отрыв пламени"));
        }
    }
}
