using Android.Graphics;
using TensorFlow.Lite.Support.Image;
using Xamarin.TensorFlow.Lite;
using Integer = Java.Lang.Integer;
using Object = Java.Lang.Object;

namespace CameraXTfLite
{
    //
    // Helper class used to communicate between our app and the TF object detection model
    //
    public class ObjectDetectionHelper
    {
        // Abstraction object that wraps a prediction output in an easy to parse way
        public class ObjectPrediction
        { public RectF Location; public string Label; public float Score; }

        private float[][][] locations = new float[1][][] { new float[ObjectCount][] };
        private float[][] labelIndices = new float[1][] { new float[ObjectCount] };
        private float[][] scores = new float[1][] { new float[ObjectCount] };

        private Object Locations, LabelIndices, Scores;
        private IDictionary<Integer, Object> outputBuffer;

        public ObjectDetectionHelper(Interpreter tflite, IList<string> labels)
        {
            this.tflite = tflite;
            this.labels = labels;

            for (int i = 0; i < ObjectCount; i++)
            {
                locations[0][i] = new float[4];
            }

            Locations = Object.FromArray(locations);
            LabelIndices = Object.FromArray(labelIndices);
            Scores = Object.FromArray(scores);

            outputBuffer = new Dictionary<Integer, Object>()
            {
#pragma warning disable CA1422
                [new Integer(0)] = Locations,
                [new Integer(1)] = LabelIndices,
                [new Integer(2)] = Scores,
                [new Integer(3)] = new float[1],
#pragma warning restore CA1422
            };
        }

        private Interpreter tflite;
        private IList<string> labels;

        private ObjectPrediction[] Predictions()
        {
            var objectPredictions = new ObjectPrediction[ObjectCount];
            for (int i = 0; i < ObjectCount; i++)
            {
                objectPredictions[i] = new ObjectPrediction
                {
                    // The locations are an array of [0, 1] floats for [top, left, bottom, right]
                    Location = new RectF(
                        locations[0][i][1], locations[0][i][0],
                        locations[0][i][3], locations[0][i][2]),

                    Label = labels[(int) labelIndices[0][i]],

                    // Score is a single value of [0, 1]
                    Score = scores[0][i]
                };
            }
            return objectPredictions;
        }

        public ObjectPrediction[] Predict(TensorImage image)
        {
            tflite.RunForMultipleInputsOutputs(new Object[] { image.Buffer }, outputBuffer);

            locations = Locations.ToArray<float[][]>();
            labelIndices = LabelIndices.ToArray<float[]>();
            scores = Scores.ToArray<float[]>();

            return Predictions();
        }

        private const int ObjectCount = 10;
    }
}
