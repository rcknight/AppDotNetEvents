using System;

namespace PostImporter
{
    [Serializable]
    public class PauseException : Exception
    {
        public int Pause { get; set; }


        public PauseException(int pause)
        {
            Pause = pause;
        }

        public PauseException(string message, int pause) : base(message)
        {
            Pause = pause;
        }

        public PauseException(string message, Exception inner, int pause) : base(message, inner)
        {
            Pause = pause;
        }
    }
}