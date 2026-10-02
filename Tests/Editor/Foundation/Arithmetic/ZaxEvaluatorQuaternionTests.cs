using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using KusakaFactory.Zatools.Foundation.Arithmetic;
using static KusakaFactory.Zatools.Tests.Foundation.Arithmetic.Zax;

namespace KusakaFactory.Zatools.Tests.Foundation.Arithmetic
{
    public sealed class ZaxEvaluatorQuaternionTests
    {
        private static readonly float Diagonal = math.sqrt(0.5f);

        private static IEnumerable<TestCaseData> RotationCases()
        {
            yield return new TestCaseData("Q_IDENTITY 1 2 3 vec3 Qrotate", F3(1, 2, 3));
            yield return new TestCaseData("Z_AXIS HALF_PI Qaxisangle X_AXIS Qrotate", F3(0, 1, 0));
            yield return new TestCaseData("X_AXIS 1 Qaxisangle X_AXIS Qrotate", F3(1, 0, 0));
            yield return new TestCaseData("Z_AXIS PI 4 / Qaxisangle dup Qmul X_AXIS Qrotate", F3(0, 1, 0));
            yield return new TestCaseData("Z_AXIS HALF_PI Qaxisangle X_AXIS HALF_PI Qaxisangle Qmul Y_AXIS Qrotate", F3(0, 0, 1));
            yield return new TestCaseData("X_AXIS HALF_PI Qaxisangle Z_AXIS HALF_PI Qaxisangle Qmul Y_AXIS Qrotate", F3(-1, 0, 0));
            yield return new TestCaseData("Z_AXIS HALF_PI Qaxisangle Qinverse X_AXIS Qrotate", F3(0, -1, 0));
            yield return new TestCaseData("Z_AXIS HALF_PI Qaxisangle Qconjugate X_AXIS Qrotate", F3(0, -1, 0));
            yield return new TestCaseData("0 0 HALF_PI vec3 Qeuler X_AXIS Qrotate", F3(0, 1, 0));
            yield return new TestCaseData("X_AXIS Y_AXIS Qlook Z_AXIS Qrotate", F3(1, 0, 0));
            yield return new TestCaseData("Q_IDENTITY Z_AXIS HALF_PI Qaxisangle 0.5 Qslerp X_AXIS Qrotate", F3(Diagonal, Diagonal, 0));
            yield return new TestCaseData("Q_IDENTITY Z_AXIS HALF_PI Qaxisangle 0.5 Qnlerp X_AXIS Qrotate", F3(Diagonal, Diagonal, 0));
            yield return new TestCaseData("Q_IDENTITY Z_AXIS HALF_PI Qaxisangle 1 Qslerp X_AXIS Qrotate", F3(0, 1, 0));
        }

        [TestCaseSource(nameof(RotationCases))]
        public void RotatesVectors(string source, ZaxValue expected)
        {
            AreEqual(expected, Evaluate(source));
        }

        private static IEnumerable<TestCaseData> QuaternionResultCases()
        {
            yield return new TestCaseData("Z_AXIS 0.5 Qaxisangle dup Qinverse Qmul", F4(0, 0, 0, 1));
            yield return new TestCaseData("Z_AXIS HALF_PI Qaxisangle", F4(0, 0, Diagonal, Diagonal));
            yield return new TestCaseData("Z_AXIS 1 Qaxisangle", ZaxValue.FromFloat4(quaternion.AxisAngle(new float3(0, 0, 1), 1.0f).value));
            yield return new TestCaseData("0.1 0.2 0.3 vec3 Qeuler", ZaxValue.FromFloat4(quaternion.Euler(0.1f, 0.2f, 0.3f).value));
            yield return new TestCaseData("1 2 3 4 vec4 Qconjugate", F4(-1, -2, -3, 4));
            yield return new TestCaseData("Z_AXIS Y_AXIS Qlook", F4(0, 0, 0, 1));
        }

        [TestCaseSource(nameof(QuaternionResultCases))]
        public void ProducesQuaternions(string source, ZaxValue expected)
        {
            AreEqual(expected, Evaluate(source));
        }
    }
}
