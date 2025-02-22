
namespace UdonSharp.Serialization
{
    internal class UserClassFormatter<T> : Formatter<T>
    {
        public override void Read(ref T targetObject, IValueStorage sourceObject)
        {
            
        }

        public override void Write(IValueStorage targetObject, T sourceObject)
        {
            throw new System.NotImplementedException();
        }
    }
}