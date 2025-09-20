namespace EditorCore
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("For now, there is no server mode support. Run any interface to enter editor.");
        }
    }


    public class Core: CoreInterface.ICoreInterface
    {
        public void Hello()
        {
            Console.WriteLine("Hello!");
        }
    }
}
