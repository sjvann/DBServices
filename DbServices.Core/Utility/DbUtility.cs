namespace DbServices.Core.Utility
{
    public static class DbUtility
    {
        public static void CopyDbFile(string sourceFile, string destinateFile)
        {
            try
            {
                File.Copy(sourceFile, destinateFile, true);

            }
            catch (IOException e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
