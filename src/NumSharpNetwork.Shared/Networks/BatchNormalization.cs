using System;
using System.IO;
using Numpy;
using NumSharpNetwork.Shared.Optimizers;

namespace NumSharpNetwork.Shared.Networks
{
    public class BatchNormalizationRecord
    {
        public NDarray Gamma { get; set; }
        public NDarray Beta { get; set; }
        public NDarray Mean { get; set; }
        public NDarray Variance { get; set; }
        public NDarray StandardDerivation { get; set; }
        public NDarray StandardScore { get; set; }
        public NDarray Input { get; set; }
        public NDarray NormalizedInput { get; set; }
    }

    // Warning: this implementation might encounter overflow issuses
    public class BatchNormalization : ILayer
    {
        public string Name { get; set; }
        public bool IsTrainMode { get; set; } = true;
        // public int InputChannels { get; set; }
        private IOptimizer Optimizer { get; set; }
        public double Momentum { get; set; } = 0.9;
        public bool IsSpatial { get; set; } = false;

        // learnable parameter gamma and beta to restore the expression of the original dataset
        private NDarray Gamma { get; set; }
        private NDarray Beta { get; set; }

        private NDarray RunningMean { get; set; }
        private NDarray RunningVariance { get; set; }

        // in case variance being zero
        private double Epsilon { get; set; } = 0.00001;


        private BatchNormalizationRecord Record { get; set; } = new BatchNormalizationRecord();

        public BatchNormalization(int inputChannels, IOptimizer optimizer)
        {
            // this.InputChannels = inputChannels;
            this.Optimizer = optimizer;

            this.Gamma = np.ones(inputChannels);
            this.Beta = np.zeros(inputChannels);

            this.RunningMean = np.zeros(inputChannels);
            this.RunningVariance = np.zeros(inputChannels);
        }

        public NDarray BackPropagate(NDarray lossResultGradient)
        {
            // compress the result
            int[] inputShape = lossResultGradient.shape.Dimensions;
            if (this.IsSpatial)
            {
                lossResultGradient = CompressInput(lossResultGradient);
            }

            NDarray lossBetaGradient = lossResultGradient.sum(0);
            NDarray lossGammaGradient = (lossResultGradient * this.Record.StandardScore).sum(0);

            // d_loss / d_x
            int batchSize = lossResultGradient.shape[0];
            double meanInputGradient = 1.0 / batchSize;
            double varianceInputGradient = (2.0 / batchSize) * (1 - meanInputGradient);
            NDarray resultInputGradient =
                this.Gamma *
                (
                    (
                        (this.Record.Input - this.Record.Mean) *
                        (-0.5 * Math.Pow(varianceInputGradient - this.Epsilon, -3 / 2))
                    ) +
                    (
                        (1 - meanInputGradient) *
                        (
                            1.0 / np.sqrt(this.Record.Variance - this.Epsilon)
                        )
                    )
                );
            NDarray lossInputGradient = lossResultGradient * resultInputGradient;

            this.Beta = this.Optimizer.Optimize(this.Beta, lossBetaGradient, isAddRegularization: false);
            this.Gamma = this.Optimizer.Optimize(this.Gamma, lossGammaGradient, isAddRegularization: false);

            // restore the input
            if (this.IsSpatial)
            {
                lossInputGradient = ExpandInput(lossInputGradient, inputShape);
            }

            return lossInputGradient;
        }

        public NDarray FeedForward(NDarray input)
        {
            // compress the input
            int[] inputShape = input.shape.Dimensions;
            if (this.IsSpatial)
            {
                input = CompressInput(input);
            }
            // standard score
            NDarray mean = input.mean(0);
            NDarray variance = input.var(0);
            NDarray standardDerivation = np.sqrt(variance + this.Epsilon);
            NDarray standardScore = (input - mean.reshape(1, -1)) / standardDerivation.reshape(1, -1);

            // batch normalization
            NDarray batchNormalizedInput = standardScore * this.Gamma.reshape(1, -1) + this.Beta.reshape(1, -1);

            // save
            this.Record.Beta = this.Beta;
            this.Record.Gamma = this.Gamma;
            this.Record.Input = input;
            this.Record.NormalizedInput = batchNormalizedInput;
            this.Record.Mean = mean;
            this.Record.Variance = variance;
            this.Record.StandardDerivation = standardDerivation;
            this.Record.StandardScore = standardScore;

            this.RunningMean = mean * (1 - this.Momentum) + this.RunningMean * this.Momentum;
            this.RunningVariance = variance * (1 - this.Momentum) + this.RunningVariance * this.Momentum;

            // restore the result
            if (this.IsSpatial)
            {
                batchNormalizedInput = ExpandInput(batchNormalizedInput, inputShape);
            }

            return batchNormalizedInput;
        }

        public void Load(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
            string statePath = Path.Combine(folderPath, $"{this.Name}.npy");
            string gammaPath = $"{statePath}.gamma.npy";
            string betaPath = $"{statePath}.beta.npy";
            if (File.Exists(gammaPath))
            {
                this.Gamma = np.load(gammaPath);
            }
            if (File.Exists(betaPath))
            {
                this.Beta = np.load(betaPath);
            }
        }

        public void Save(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
            string statePath = Path.Combine(folderPath, $"{this.Name}.npy");
            np.save($"{statePath}.gamma.npy", this.Gamma);
            np.save($"{statePath}.beta.npy", this.Beta);
        }

        // input.shape = [batchSize, inputChannels, height, width]
        // return shape = [batchSize * height * width, inputChannels]
        private NDarray CompressInput(NDarray input)
        {
            int batchSize = input.shape[0];
            int inputChannels = input.shape[1];
            // transposed.shape = [batchSize, height, width, inputChannels]
            NDarray transposed = np.transpose(input, new int[] { 0, 2, 3, 1 });
            return transposed.reshape(-1, inputChannels);
        }

        private NDarray ExpandInput(NDarray compressedInput, int[] inputShape)
        {
            int batchSize = inputShape[0];
            int inputChannels = inputShape[1];
            int height = inputShape[2];
            int width = inputShape[3];

            NDarray transposed = compressedInput.reshape(batchSize, height, width, inputChannels);
            NDarray expandedInput = np.transpose(transposed, new int[] { 0, 3, 1, 2 });
            return expandedInput;
        }
    }
}