
namespace UdonSharp.Tests
{
    public class ClassB : TestInheritanceClassBase
    {
        public override string GetClassName()
        {
            return "B";
        }

        public override int GetClassID()
        {
            return 2;
        }
    }
}