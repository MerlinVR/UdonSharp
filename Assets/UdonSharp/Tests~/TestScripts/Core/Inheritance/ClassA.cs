
namespace UdonSharp.Tests
{
    public class ClassA : TestInheritanceClassBase
    {
        public override string GetClassName()
        {
            return "A";
        }

        public override int GetClassID()
        {
            return 1;
        }
    }
}
