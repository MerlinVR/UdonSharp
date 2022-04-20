
namespace UdonSharp.Tests
{
    public class ClassC : ClassB
    {
        public override string GetClassName()
        {
            return base.GetClassName() + "C";
        }
    }
}