using NUnit.Framework;

namespace FlareSystem.Tests
{
    public class FlareConstantsTests
    {
        [Test]
        public void EvaluateRecord_FlameLiftConditionProducesAlarm()
        {
            var record = new FlareDataRecord
            {
                N = 1,
                P_flare = 0.012f,
                Q_flare = 2f,
                P_purge = 0.22f,
                Q_purge = 18f,
                T_flame = 700f,
                Steam_Q = 80f,
                otriv = 0,
                hlopok = 0
            };

            FlareSystemState state = FlareConstants.EvaluateRecord(record, FlareMode.Archive, 0.2f);

            Assert.That(state.IsAlarm, Is.True);
            Assert.That(state.RiskLevel, Is.EqualTo(RiskLevel.Alarm));
            Assert.That(state.ViolationsText, Does.Contain("Высокий риск отрыва"));
        }

        [Test]
        public void EvaluateRecord_NormalRecordIsNormal()
        {
            var record = new FlareDataRecord
            {
                N = 2,
                P_flare = 0.01f,
                Q_flare = 2.5f,
                P_purge = 0.42f,
                Q_purge = 34f,
                T_flame = 980f,
                Steam_Q = 95f,
                otriv = 0,
                hlopok = 0
            };

            FlareSystemState state = FlareConstants.EvaluateRecord(record, FlareMode.Archive, 0.05f);

            Assert.That(state.IsAlarm, Is.False);
            Assert.That(state.RiskLevel, Is.EqualTo(RiskLevel.Normal));
            Assert.That(state.Violations, Is.Empty);
        }
    }
}
