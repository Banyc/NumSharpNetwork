using Numpy;

namespace NumSharpNetwork.Shared.Networks
{
    public class ReLULayer : ILayer
    {
        public string Name { get; set; } = "ReLU";
        public bool IsTrainMode { get; set; }
        private NDarray PreviousInput { get; set; }

        public NDarray BackPropagate(NDarray lossResultGradient)
        {
            NDarray resultInputGradient = np.where(this.PreviousInput <= 0, np.asarray(0), np.asarray(1));

            NDarray lossInputGradient = lossResultGradient * resultInputGradient;
            return lossInputGradient;
        }

        public NDarray FeedForward(NDarray input)
        {
            // DEBUG
            // check
            // if ((input <= 0).all())
            // {
            //     throw new System.Exception("All input elements are negative!");
            // }
            this.PreviousInput = input;
            NDarray result = np.maximum(np.asarray(0), input);
            return result;
        }

        public void Load(string folderPath)
        {
        }

        public void Save(string folderPath)
        {
        }

        public override string ToString()
        {
            return $"{this.Name}";
        }
    }
}
