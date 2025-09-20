namespace SDL2Interface
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // how write this?
            CoreInterface.ICoreInterface ICoreInterfaceRealization = EditorCore.Core;
            ICoreInterfaceRealization.Hello();
        }
    }
}
