using System.Collections.Generic;
using Android.Graphics;
using Xamarin.TensorFlow.Lite;

namespace CameraXTfLite
{
    //
    // Helper class used to communicate between our app and the TF object detection model
    //
    public class ObjectDetectionHelper
    {
        // Abstraction object that wraps a prediction output in an easy to parse way
        public class ObjectPrediction
        {
            public RectF Location;
            public string Label;
            public float Score;
        }

        private float[][][] locations = new float[1][][] { new float[ObjectCount][] };
        private float[][] labelIndices = new float[1][] { new float[ObjectCount] };
        private float[][] scores = new float[1][] { new float[ObjectCount] };

        private Java.Lang.Object Locations, LabelIndices, Scores;
        private IDictionary<Java.Lang.Integer, Java.Lang.Object> outputBuffer;

        public ObjectDetectionHelper(Interpreter tflite, List<string> labels)
        {
            this.tflite = tflite;
            this.labels = labels;

            for (int i = 0; i < ObjectCount; i++)
            {
                locations[0][i] = new float[4];
            }

            Locations = Java.Lang.Object.FromArray(locations);
            LabelIndices = Java.Lang.Object.FromArray(labelIndices);
            Scores = Java.Lang.Object.FromArray(scores);

            outputBuffer = new Dictionary<Java.Lang.Integer, Java.Lang.Object>()
            {
                [new Java.Lang.Integer(0)] = Locations,
                [new Java.Lang.Integer(1)] = LabelIndices,
                [new Java.Lang.Integer(2)] = Scores,
                [new Java.Lang.Integer(3)] = new float[1],
            };
        }

        private Interpreter tflite;
        private List<string> labels;

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

                    // SSD Mobilenet V1 Model assumes class 0 is background class
                    // in label file and class labels start from 1 to number_of_classes + 1,
                    // while outputClasses correspond to class index from 0 to number_of_classes
                    Label = labels[1 + (int) labelIndices[0][i]],

                    // Score is a single value of [0, 1]
                    Score = scores[0][i]
                };
            }
            return objectPredictions;
        }

        public ObjectPrediction[] Predict(Java.Nio.ByteBuffer buffer)
        {
            tflite.RunForMultipleInputsOutputs(new Java.Lang.Object[] { buffer }, outputBuffer);

            locations = Locations.ToArray<float[][]>();
            labelIndices = LabelIndices.ToArray<float[]>();
            scores = Scores.ToArray<float[]>();

            return Predictions();
        }

        private const int ObjectCount = 10;
    }
}
