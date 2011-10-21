using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Research.Kinect.Nui;
using System.Collections;
//using System.Timers;
using System.Diagnostics;
using System.Windows.Forms;

using System.Windows.Threading;
//using System.Windows.Threading.DispatcherTimer;





namespace KinectGesture_ArmsPosition
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        Runtime nui;
        int totalFrames = 0;
        int lastFrames = 0;
        DateTime lastTime = DateTime.MaxValue;
        
        // We want to control how depth data gets converted into false-color data
        // for more intuitive visualization, so we keep 32-bit color frame buffer versions of
        // these, to be updated whenever we receive and process a 16-bit frame.
        const int RED_IDX = 2;
        const int GREEN_IDX = 1;
        const int BLUE_IDX = 0;
        byte[] depthFrame32 = new byte[320 * 240 * 4];
        ArrayList sequence= new ArrayList();
        private DateTime _captureCountdown = DateTime.Now;
        ArrayList input = new ArrayList();
        bool liveCapturing = false;
        bool capture = false;
        bool processing = false;
        int[] previousJoint = new int[20];

        ArrayList[][] recordedAngle = new ArrayList[20][]; //Saves the Angles of the gesture you record
        ArrayList[][] recordedVectorAngle = new ArrayList[20][]; //Saves the vectors' directions of the gesture you record
        ArrayList[][] boundaryRecordedAngle = new ArrayList[20][]; //Saves the Angles of the gesture with highest/lowest boundary you record
        ArrayList[][] boundaryRecordedVectorAngle = new ArrayList[20][]; //Saves the vectors' directions of the gesture with highest/lowest boundary you record

        ArrayList[] currentAngle = new ArrayList[20]; // Saves the angle of the real-time data
        ArrayList[] currentVectorAngle = new ArrayList[20]; // Saves the vector direction of the real-time data

        int[] alreadyInQueue = new int[200];// 200 is define as the highest number of gestures in database
        ArrayList[] importantPoints = new ArrayList[20]; // This variable hold the important points of each gesture
        bool captureProcessingDone = false;
        double[][] range=new double[20][];
        bool lowerBoundaryDone = false;
        double[][] score = new double[5000][];
        double penalty = 5;
        double[] userCode_Direction = new double[5000];// includes the recorded directions in the current real time segmentation
        double[] gestureCodePureAngle = new double[5000];// includes the saved directions for the gesture in database
        double[] userCodePureAngle = new double[5000]; // includes the recorded angles in the current real time segmentation
        double[] gestureCode_Direction = new double[5000];// includes the saved angles for the gesture in database
        double[][] path = new double[5000][]; // This is a debugging variable for the alignment method,using this variable you can find the best match's path. Just ignore it for now
        String gestureInfo = ""; // We use this variable in Saving data to file
        Timer _captureCountdownTimer;
        
        int dataBaseCounter = 0;

        Dictionary<JointID, Brush> jointColors = new Dictionary<JointID, Brush>() { 
            {JointID.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {JointID.Head, new SolidColorBrush(Color.FromRgb(200, 0,   0))},
            {JointID.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79,  84,  33))},
            {JointID.ElbowLeft, new SolidColorBrush(Color.FromRgb(84,  33,  42))},
            {JointID.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HandLeft, new SolidColorBrush(Color.FromRgb(215,  86, 0))},
            {JointID.ShoulderRight, new SolidColorBrush(Color.FromRgb(33,  79,  84))},
            {JointID.ElbowRight, new SolidColorBrush(Color.FromRgb(33,  33,  84))},
            {JointID.WristRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.HandRight, new SolidColorBrush(Color.FromRgb(37,   69, 243))},
            {JointID.HipLeft, new SolidColorBrush(Color.FromRgb(77,  109, 243))},
            {JointID.KneeLeft, new SolidColorBrush(Color.FromRgb(69,  33,  84))},
            {JointID.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {JointID.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {JointID.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222,  76))},
            {JointID.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {JointID.FootRight, new SolidColorBrush(Color.FromRgb(77,  109, 243))}

           

        };
    
        private void Window_Loaded(object sender, EventArgs e)
        {
            nui = new Runtime();

            #region previousJoints Assigning the start and end points that define bones
            // Later on we want to calculate the angle that each "Bone" is making with the x-axis. Each bone consists of two joints
            // for example the Elbow bone is the line between Elbow joint and Should Joint. So we need to have the Previous Joint for
            // each of the joint so that we can define a bone for that. previousJoin[2]=3 means that previous Joint point of the point 2 (which is the left elbow) is point 3 which is left shoulder
            previousJoint[0] = 1;
            previousJoint[1] = 2;
            previousJoint[2] = 3;

            previousJoint[3] = 8;

            previousJoint[4] = 5;
            previousJoint[5] = 6;
            previousJoint[6] = 7;
            previousJoint[7] = 8;
            previousJoint[8] = 19;
            previousJoint[9] = 8;

            previousJoint[10] = 11;
            previousJoint[11] = 12;
            previousJoint[12] = 13;
            previousJoint[13] = 14;

            previousJoint[14] = 19; // Hipcenter -> Spine
            previousJoint[15] = 16;
            previousJoint[16] = 17;
            previousJoint[17] = 18;
            previousJoint[18] = 14;
            previousJoint[19] = 14; // Spine -> Hipcenter

            #endregion
          
            // Initialization
            for (int i = 0; i != 20; i++)
                importantPoints[i] = new ArrayList();

            for (int i = 0; i != 5000; i++)
                score[i] = new double[5000];

            for (int i = 0; i != 5000; i++)
                path[i] = new double[5000];

            for (int point = 0; point != 20; point++)
            {
                recordedAngle[point] = new ArrayList[400]; // 0 is the database capture number
                recordedAngle[point][dataBaseCounter] = new ArrayList();
                recordedVectorAngle[point] = new ArrayList[400];
                recordedVectorAngle[point][dataBaseCounter] = new ArrayList();

                boundaryRecordedAngle[point] = new ArrayList[400]; // 0 is the database capture number
                boundaryRecordedAngle[point][dataBaseCounter] = new ArrayList();
                boundaryRecordedVectorAngle[point] = new ArrayList[400];
                boundaryRecordedVectorAngle[point][dataBaseCounter] = new ArrayList();

                currentAngle[point] = new ArrayList();
                currentVectorAngle[point] = new ArrayList();
     
                range[point] = new double[400];
                range[point][dataBaseCounter] = 0;
            }

                      
            try
            {
                nui.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);
            }
            catch (InvalidOperationException)
            {
               
                System.Windows.MessageBox.Show("Runtime initialization failed. Please make sure Kinect device is plugged in.");
                
                return;
            }
            
            
            try
            {
                nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                return;
            }
            
               
            lastTime = DateTime.Now;

            nui.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady);
            nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
            nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_ColorFrameReady);

        

        }

        void nui_ColorFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage Image = e.ImageFrame.Image;
            video.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, Image.Bits, Image.Width * Image.BytesPerPixel);
        }

        void nui_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage Image = e.ImageFrame.Image;
            byte[] convertedDepthFrame = convertDepthFrame(Image.Bits);

            depth.Source = BitmapSource.Create(
                Image.Width, Image.Height, 96, 96, PixelFormats.Bgr32, null, convertedDepthFrame, Image.Width * 4);

            ++totalFrames;

            DateTime cur = DateTime.Now;
            if (cur.Subtract(lastTime) > TimeSpan.FromSeconds(1))
            {
                int frameDiff = totalFrames - lastFrames;
                lastFrames = totalFrames;
                lastTime = cur;
                frameRate.Text = frameDiff.ToString() + " fps";
            }
        }

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonFrame = e.SkeletonFrame;
            int iSkeleton = 0;
            
            Brush[] brushes = new Brush[6];
            brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
            brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
            brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
            brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));
            
            skeleton.Children.Clear();
            foreach (SkeletonData data in skeletonFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    // Draw bones
                    Brush brush = brushes[iSkeleton % brushes.Length];
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.Spine, JointID.ShoulderCenter, JointID.Head));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderLeft, JointID.ElbowLeft, JointID.WristLeft, JointID.HandLeft));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderRight, JointID.ElbowRight, JointID.WristRight, JointID.HandRight));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipLeft, JointID.KneeLeft, JointID.AnkleLeft, JointID.FootLeft));
                    skeleton.Children.Add(getBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipRight, JointID.KneeRight, JointID.AnkleRight, JointID.FootRight));
                    var p = new Point[20];
                    // Draw joints
                    foreach (Joint joint in data.Joints)
                    {
                        Point jointPos = getDisplayPosition(joint);
                        Line jointLine = new Line();
                        jointLine.X1 = jointPos.X - 3;
                        jointLine.X2 = jointLine.X1 + 6;
                        jointLine.Y1 = jointLine.Y2 = jointPos.Y;
                        jointLine.Stroke = jointColors[joint.ID];
                        jointLine.StrokeThickness = 6;
                        skeleton.Children.Add(jointLine);

                    switch (joint.ID)
                    {
                        case JointID.HandLeft:
                            p[0] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.WristLeft:
                            p[1] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.ElbowLeft:
                            p[2] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.ShoulderLeft:
                            p[3] = new Point(joint.Position.X, joint.Position.Y);
                            break;


                        case JointID.HandRight:
                            p[4] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.WristRight:
                            p[5] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.ElbowRight:
                            p[6] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.ShoulderRight:
                            p[7] = new Point(joint.Position.X, joint.Position.Y);
                            break;

                        case JointID.ShoulderCenter:
                            p[8] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.Head:
                            p[9] = new Point(joint.Position.X, joint.Position.Y);
                            break;


                        case JointID.FootLeft:
                            p[10] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.AnkleLeft:
                            p[11] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.KneeLeft:
                            p[12] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.HipLeft:
                            p[13] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.HipCenter:
                            p[14] = new Point(joint.Position.X, joint.Position.Y);
                            break;

                        case JointID.FootRight:
                            p[15] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.AnkleRight:
                            p[16] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.KneeRight:
                            p[17] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                        case JointID.HipRight:
                            p[18] = new Point(joint.Position.X, joint.Position.Y);
                            break;


                        case JointID.Spine:
                            p[19] = new Point(joint.Position.X, joint.Position.Y);
                            break;
                     
                           }        
                    
                    }

                    if (capture) // If we are in the Recording phase,add the joints data to the sequence variable
                    {
                        sequence.Add(p);
                    }

                    if (sequence.Count > 80 && !processing) // PROCESSING THE CAPTURED SEQUENCE
                    {
                        status.Text = "Captured!";
                        capture = false;
                        processing = true;


                        #region Calculate The Angle that each bone is making with the x-axis
                        for (int pointNum = 0; pointNum != 7; pointNum++)
                        {
                            for (int i = 0; i != sequence.Count; i++)
                            {
                                double yDif = ((Point[])sequence[i])[pointNum].Y - ((Point[])sequence[i])[previousJoint[pointNum]].Y;
                                double xDif = ((Point[])sequence[i])[pointNum].X - ((Point[])sequence[i])[previousJoint[pointNum]].X;
                                double d1 = new double();
                                if (xDif != 0) d1 = (Math.Atan(yDif / xDif) * 180 / Math.PI);
                                else if (yDif > 0) d1 = 90;
                                else if (yDif < 0) d1 = -90;
                                else d1 = 0;

                                recordedAngle[pointNum][dataBaseCounter].Add((double)(int)d1);
                           

                            }
                           
                        #endregion

                        #region Calculate the Direction of the Vectors between frames
                            int zeroCounter = 0;
                            int zeroStartIndex = 0;
                            //Each Joint is compared to ITSELF in 15 frames later and a vector is drawn from the joint in frame x1 to the same joint in frame x1+1
                            for (int i = 0; i != sequence.Count - 15; i++)
                            {
                                //vector direction is calculated using the formula : (y2-y2/x2-x1) y2 and x2 are the positions of the same joints in 15 frames later than x1 and y1
                                double xFrameDif = ((Point[])sequence[i])[pointNum].X - ((Point[])sequence[i + 15])[pointNum].X;
                                double yFrameDif = ((Point[])sequence[i])[pointNum].Y - ((Point[])sequence[i + 15])[pointNum].Y;

                                double angle = new double();
                                if (xFrameDif != 0) angle = Math.Atan(yFrameDif / xFrameDif) * (180 / Math.PI);
                                else if (yFrameDif > 0) angle = 90;
                                else if (yFrameDif < 0) angle = -90;

                                if (yFrameDif == 0 && xFrameDif >= 0) angle = 0;
                                if (yFrameDif == 0 && xFrameDif < 0) angle = 180;
                                angle = Math.Round(angle, 2);

                                //We calculate the vector size 
                                double vectorSize = Math.Sqrt(Math.Pow(xFrameDif, 2) + Math.Pow(yFrameDif, 2));
                                vectorSize = (int)(vectorSize * 100);
                                
                                // I want to trim the data that I record,because many times the users stays still
                                // at the first frames or last frames and it's not part of the gesture
                                // So I need to find the constant points in the movements

                                if (vectorSize <= 5)
                                {
                                    //If the vector size was less than 5 it means that the joints hasn't moved within 15 frames but
                                    // we still cannot say it's a constant point because it might be caused by noises, so we wait for 
                                    // 4 more Zeros to receive to say that the point has been constant
                                    if (zeroCounter == 0) zeroStartIndex = recordedVectorAngle[pointNum][dataBaseCounter].Count-1; // I save the frame number that the Zeros has started in, so that if the zeros were
                                    //more than 4 I can just trim the data from the starting frame
                                    if (zeroStartIndex < 0) zeroStartIndex = 0;
                                    recordedVectorAngle[pointNum][dataBaseCounter].Add(400.0); // 400 is the number I assign instead of Zero
                                    zeroCounter++;
                                }
                                else
                                {
                                    // If the Zeros were disturbed in the middle, I say zeroCounter-=2 but I do not say Zero Counter=0
                                    // because the Non-Zero number could be a noise. for example I have Zeros in 000100 and they have been disturbed by a 1
                                    // I want to be able to trim that as well.
                                    zeroCounter -= 2;
                                    if (zeroCounter < 0) zeroCounter = 0; // Sentinel
                                    recordedVectorAngle[pointNum][dataBaseCounter].Add(angle);
                                    
                                    // If the point has not been a constant point I consider it as an important point in the gesture
                                    // Writing this comment I just realized that I need to enhance this part a little because some noises might
                                    //be added here as well
                                    if (!importantPoints[dataBaseCounter].Contains(pointNum))
                                    {
                                        importantPoints[dataBaseCounter].Add(pointNum);
                                    }
                                }
                                if (zeroCounter >= 4)
                                {
                                    // Here we are ! We just recognized some non-moving event happening ! We don't need it
                                    // Lets just Trim !
                                    recordedVectorAngle[pointNum][dataBaseCounter].RemoveRange(zeroStartIndex, 4);
                                    recordedAngle[pointNum][dataBaseCounter].RemoveRange(zeroStartIndex, 4);
                                    zeroCounter = 0;
                                    zeroStartIndex = 0;
                                }

                            }


                        }
                        #endregion

                        // Processing The Captured data is finished here so we prepare for receiving the boundaries
                        wait(40000000);
                        
                        status.Text = "Upper Boundary";
                        sequence.Clear();
                        captureProcessingDone = true;
                        capture = true;
                        txtSavedVectorAngles.Clear();
                        txtSavedAngles.Clear();
                       
                        dataBaseCounter++;
                     
                        
                    }

                    if (captureProcessingDone && sequence.Count > 80)
                    {
                        status.Text = "Captured!";
                        capture = false;
                        processing = true;
                        captureProcessingDone = false;
                        #region Calculate The Angle that each bone is making with the x-axis
                        for (int pointNum = 0; pointNum != 7; pointNum++)
                        {
                            for (int i = 0; i != sequence.Count; i++)
                            {
                                double yDif = ((Point[])sequence[i])[pointNum].Y - ((Point[])sequence[i])[previousJoint[pointNum]].Y;
                                double xDif = ((Point[])sequence[i])[pointNum].X - ((Point[])sequence[i])[previousJoint[pointNum]].X;
                                double d1 = new double();
                                if (xDif != 0) d1 = (Math.Atan(yDif / xDif) * 180 / Math.PI);
                                else if (yDif > 0) d1 = 90;
                                else if (yDif < 0) d1 = -90;
                                else d1 = 0;

                                boundaryRecordedAngle[pointNum][dataBaseCounter-1].Add((double)(int)d1); // TO DO: fix
                        
                            }
                        #endregion
                        #region Calculate the Direction of the Vectors between frames
                            for (int i = 0; i != sequence.Count - 15; i++)
                            {
                                double xFrameDif = ((Point[])sequence[i])[pointNum].X - ((Point[])sequence[i + 15])[pointNum].X;
                                double yFrameDif = ((Point[])sequence[i])[pointNum].Y - ((Point[])sequence[i + 15])[pointNum].Y;

                                double angle = new double();
                                if (xFrameDif != 0) angle = Math.Atan(yFrameDif / xFrameDif) * (180 / Math.PI);
                                else if (yFrameDif > 0) angle = 90;
                                else if (yFrameDif < 0) angle = -90;

                                if (yFrameDif == 0 && xFrameDif >= 0) angle = 0;
                                if (yFrameDif == 0 && xFrameDif < 0) angle = 180;

                                double tool = Math.Sqrt(Math.Pow(xFrameDif, 2) + Math.Pow(yFrameDif, 2));

                               
                                tool = (int)(tool * 100);
                       
                                angle = Math.Round(angle, 2);
                                if (tool <= 5)
                                {
                                    boundaryRecordedVectorAngle[pointNum][dataBaseCounter - 1].Add(400.0);
                   
                                }
                                else
                                {
                                    boundaryRecordedVectorAngle[pointNum][dataBaseCounter - 1].Add(angle);
                            
                                }
                                
                            }

                        }
                        #endregion

                        // Processing The Second Captured data is finished here, we prepare for lower boundary
                        align2(dataBaseCounter - 1, 80, 0);
                        if (!lowerBoundaryDone)
                        {
                            wait(40000000);
                            txtSavedVectorSizes.Clear();
                            status.Text = "Lower Boundary";
                            lowerBoundaryDone = true;
                            sequence.Clear();
                            captureProcessingDone = true;
                            capture = true; // NEXT level
                        }
                        // We have all the boundaries information lets just start recognizing realtime gestures
                        else
                        {
                            wait(40000000);
                            liveCapturing = true;
                            status.Text = "LIVE!";
                        }

                    }


                    #region Almost the same calculations that we did when recording the data,the different is that in that time we had all the data saved but here it just updates and we calculate everything live time
                    else if (liveCapturing)
                    {
                        input.Add(p);

                        double d1 = new double();
                        double angle = new double();
                        double tool = new double();

                        for (int pointNum = 0; pointNum != 7; pointNum++)
                        {
 
                           double xDif4Angle = ((Point[])input[input.Count - 1])[pointNum].X - ((Point[])input[input.Count - 1])[previousJoint[pointNum]].X;
                           double yDif4Angle = ((Point[])input[input.Count - 1])[pointNum].Y - ((Point[])input[input.Count - 1])[previousJoint[pointNum]].Y;

                            if (xDif4Angle != 0) d1 = (Math.Atan(yDif4Angle / xDif4Angle) * 180 / Math.PI);
                            else if (yDif4Angle > 0) d1 = 90;
                            else if (yDif4Angle < 0) d1 = -90;
                            else d1 = 0;
                            currentAngle[pointNum].Add((double)(int)d1); // for example currentAngle[2][0] is the angle of point 2 (left wrist) in the 0(first) frame
                          
                            if (input.Count > 15)
                            {
                                double xFrameDif = ((Point[])input[input.Count - 16])[pointNum].X - ((Point[])input[input.Count - 1])[pointNum].X;
                                double yFrameDif = ((Point[])input[input.Count - 16])[pointNum].Y - ((Point[])input[input.Count - 1])[pointNum].Y;

                                if (xFrameDif != 0) angle = Math.Atan(yFrameDif / xFrameDif) * (180 / Math.PI);
                                else if (yFrameDif > 0) angle = 90;
                                else if (yFrameDif < 0) angle = -90;

                                if (yFrameDif == 0 && xFrameDif >= 0) angle = 0;
                                if (yFrameDif == 0 && xFrameDif < 0) angle = 180;

                                tool = Math.Sqrt(Math.Pow(xFrameDif, 2) + Math.Pow(yFrameDif, 2));

                                tool = (int)(tool * 100);
                        

                                angle = Math.Round(angle, 2);
                                if (tool <= 5)
                                {
                                    currentVectorAngle[pointNum].Add(400.0);
                                }
                                else
                                {
                                    currentVectorAngle[pointNum].Add(angle);
                                }
                                

                            }
                        }
                    #endregion
                        #region After each frame we check whether we a gesture has been performed
                        double _Thr = 15; // TODO: CALCULATE CORRECT Threshhold
                        int correctCounter = 0;

                        // Clearing data each 400 frames
                        if (input.Count > 400)
                        {
                            input.RemoveRange(0, 400 - 70);
                            for (int i = 0; i != dataBaseCounter; i++)
                            {
                                alreadyInQueue[0] = 0;
                                alreadyInQueue[1] = 0;
                            }
                            for (int k = 0; k != 7; k++)
                            {
                                currentAngle[k].RemoveRange(0,400-70);
                                currentVectorAngle[k].RemoveRange(0,400-70);
                            }
                            textBox5.Clear();
                            gestureL.Clear();
                            textBox3.Clear();
                            for (int t = 0; t != 300; t++)
                                for (int j = 0; j != 300; j++)
                                    score[t][j] = 0;
                        }
                        // Segmentation
                        // Segmentation part is not complete,it has to be enhanced a lot more. Currently we check the first angle of the current movement with the first of the one in the database and 
                        // if they were almost equal we sent the data frame that frame to 70 frames later to the alignment method
                        // The thing that needs to be considered is that we are not just having one joint, so the first frame's angles should be checked
                        // for all the joints involved in the gesture (which are already recognized as "importantPoint"). if more than 80% of the 
                        // points satisfied the condition we segment the data.
                        
                        // NOTE on "alreadyInQueue" variable:
                        // As we are having a threshold for the equality of angles, an angle may be considered equal whenever it is within a range
                        // so for example frames x1,x1+1,x1+2 till x1+10 will satisfy the condition and starting from each of them to 70 frames later
                        // the data wil be segmented and sent to the alignment method.We just need one in every ten frames, so if another segmentation was going to
                        // be sent within 10 frames from the current frame we just ignore it.
                        for (int dataBaseRec = 0; dataBaseRec != dataBaseCounter && (input.Count - 1 > 70); dataBaseRec++) // for each Record in database check
                        {
                            correctCounter = 0;
                            if ((input.Count - alreadyInQueue[dataBaseRec] < 10 && (alreadyInQueue[dataBaseRec]) != 0)) continue;
                            for (int po = 0; po != importantPoints[dataBaseRec].Count; po++)
                            {
                                if (Math.Abs((double)(currentAngle[(int)(importantPoints[dataBaseRec][po])])[currentAngle[(int)(importantPoints[dataBaseRec][po])].Count - 1] - (double)(recordedAngle[(int)(importantPoints[dataBaseRec][po])][dataBaseRec][(recordedAngle[(int)(importantPoints[dataBaseRec][po])][dataBaseRec].Count) - 1])) < _Thr)
                                {  // if currentAngle[in noghte] dar [in lahze] az recordedAngle [hamoon noghte] dar [folan record] dar [lahzeyeh avval] ekhtelafe kami dasht:
                                    correctCounter++;

                                }
                            }
                            
                            if (correctCounter >= (int)(importantPoints[dataBaseRec].Count * 0.8)) // 80% of number of points
                            {
                                //We need to check the boundaries here
                                // IF True go on
                                double finalScore = align(dataBaseRec, input.Count - 1, input.Count - 1 -70);
                                if (finalScore == 1) textBox3.Text += dataBaseRec.ToString()+" ";
                                textBox5.Text += score.ToString();
                                gestureL.Text += input.Count;//"aligned with "+dataBaseIndex.ToString();
                                alreadyInQueue[dataBaseRec] = input.Count;

                            }
                            correctCounter = 0;
                        }

                        #endregion
                    }

                }
                     
                iSkeleton++;
            } // for each skeleton
        }

     
    
        byte[] convertDepthFrame(byte[] depthFrame16)
        {
            for (int i16 = 0, i32 = 0; i16 < depthFrame16.Length && i32 < depthFrame32.Length; i16 += 2, i32 += 4)
            {
                int player = depthFrame16[i16] & 0x07;
                int realDepth = (depthFrame16[i16 + 1] << 5) | (depthFrame16[i16] >> 3);
                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                byte intensity = (byte)(255 - (255 * realDepth / 0x0fff));

                depthFrame32[i32 + RED_IDX] = 0;
                depthFrame32[i32 + GREEN_IDX] = 0;
                depthFrame32[i32 + BLUE_IDX] = 0;

                // choose different display colors based on player
                switch (player)
                {
                    case 0:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 2);
                        break;
                    case 1:
                        depthFrame32[i32 + RED_IDX] = intensity;
                        break;
                    case 2:
                        depthFrame32[i32 + GREEN_IDX] = intensity;
                        break;
                    case 3:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 4:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity / 4);
                        break;
                    case 5:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 4);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 6:
                        depthFrame32[i32 + RED_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(intensity / 2);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(intensity);
                        break;
                    case 7:
                        depthFrame32[i32 + RED_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + GREEN_IDX] = (byte)(255 - intensity);
                        depthFrame32[i32 + BLUE_IDX] = (byte)(255 - intensity);
                        break;
                }
            }
            return depthFrame32;
        }

        private Point getDisplayPosition(Joint joint)
        {
            float depthX, depthY;
            nui.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);
            depthX = Math.Max(0, Math.Min(depthX * 320, 320));  //convert to 320, 240 space
            depthY = Math.Max(0, Math.Min(depthY * 240, 240));  //convert to 320, 240 space
            int colorX, colorY;
            ImageViewArea iv = new ImageViewArea();
            // only ImageResolution.Resolution640x480 is supported at this point
            nui.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, (short)0, out colorX, out colorY);

            // map back to skeleton.Width & skeleton.Height
            return new Point((int)(skeleton.Width * colorX / 640.0), (int)(skeleton.Height * colorY / 480));
        }

        Polyline getBodySegment(Microsoft.Research.Kinect.Nui.JointsCollection joints, Brush brush, params JointID[] ids)
        {
            PointCollection points = new PointCollection(ids.Length);
            for (int i = 0; i < ids.Length; ++i)
            {
                points.Add(getDisplayPosition(joints[ids[i]]));
            }

            Polyline polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            nui.Uninitialize();
            Environment.Exit(0);
        }
            
       
        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            //status.Text = "Waiting Three Seconds";
            wait(40000000);
            //  System.Windows.Forms.MessageBox.Show("Test");
            status.Text = "Recording";
            gestureL.Clear();
            gestureR.Clear();
            textBox1.Clear();
            textBox2.Clear();
            textBox3.Text += ">";
            textBox4.Text += ">";
            sequence.Clear();
            txtCurrentVectorAngles.Text += ">";
            txtCurrentAngles.Text += ">";
            txtCurrentVectorSizes.Text += ">";
            txtSavedAngles.Text += ">";
            txtSavedVectorAngles.Text += ">";
            txtSavedVectorSizes.Text += ">";
            textBox5.Clear();
            liveCapturing = false;
            for (int k = 0; k != 200; k++)
                alreadyInQueue[k] = 0;//200 is the most number of gestures in database
            
            for (int point = 0; point != 20; point++)
            {
    
                recordedAngle[point][dataBaseCounter] = new ArrayList();
         
                recordedVectorAngle[point][dataBaseCounter] = new ArrayList();
    
                boundaryRecordedAngle[point][dataBaseCounter] = new ArrayList();
 
                boundaryRecordedVectorAngle[point][dataBaseCounter] = new ArrayList();

                range[point][dataBaseCounter] = 0;
            }
            
            
            processing = false;
            capture = true;
         }

        private void wait(long ticks)
        {


            long dtEnd = DateTime.Now.AddTicks(ticks).Ticks;
            int count = 0;
            while (DateTime.Now.Ticks < dtEnd)
            {
                status.Text = ((count / 10000) + 1).ToString();
                count++;
                this.Dispatcher.Invoke(DispatcherPriority.Background, (DispatcherOperationCallback)delegate(object unused) { return null; }, null);

            }

        }

  
        double align(int dataBaseIndex,int endFrame,int startFrame)
        {
            // Dynamic Alignment Method
            // Whenever we segmented a real-time data it will be sent to this method and we will align it with the basic saved gesture
            // The alignment has a score and if the score was within a range that we have already defined, it will be considered as a valid gesture.
            int maxCounter = 0;
            int numberOfPoints = importantPoints[dataBaseIndex].Count;
            for (int pointNum = 0; pointNum != numberOfPoints; pointNum++)
            {
                // score[][] is the Dynamic Matrix that we fill
                // for more information on Dynamic Programming refer to "Introduction to Algorithms" By Thomas H. Cormen, Charles E. Leiserson, Ronald L. Rivest, and Clifford Stein - MIT Press
                // for more information on DTW and other dynamic alignment methods refer to Wikipedia or other papers
                // I have define a linear gap penalty and free end gaps(no penalty for gaps at start and end of the alignment)
                score[0][0] = 0;

                for (int i = 1; i != recordedVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Count+1; i++)
                {
                    gestureCode_Direction[i] = (double)(recordedVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex])[i - 1];
                   
                }
                int gestureSize = recordedVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Count;

                for (int i = startFrame+1; i != (endFrame - 15) + 1+1; i++)
                {
                    userCode_Direction[(i - startFrame)] = (double)(currentVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]])[i-1]; //indexing starts from 1 (0 is sentinel)
  
                }
                int userSize = (endFrame - 15+1) - (startFrame + 1)+1;
              
                for (int i = 1; i != recordedAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Count+1; i++)
                {
                    gestureCodePureAngle[i] = (double)(recordedAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex])[i - 1];
                  
                }
                int gestureAngleSize = recordedAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Count;

                
                for (int i = startFrame+1; i != endFrame + 1+1; i++)
                {
                    userCodePureAngle[(i - startFrame)] = (double)(currentAngle[(int)importantPoints[dataBaseIndex][pointNum]])[i-1];
                    
                }

                for (int j = 0; j != gestureCode_Direction.Length; j++) score[0][j] = 0;
                for (int j = 0; j != userCode_Direction.Length; j++) score[j][0] = 0;
               


                int previousGap1 = 1;
                int previousGap2 = 1;
                
                for (int i = 0; i != 5000; i++)
                {
                    path[0][i] = 2;
                    path[i][0] = 1;
                }

                for (int j = 1; j != gestureSize + 1; j++)
                    for (int i = 1; i != userSize + 1; i++)
                    {

                        double max = -50000;

                        if (j == gestureSize)
                        {
                            if (score[i - 1][j] > score[i][j - 1] - (penalty * previousGap2))
                            {
                                max = score[i - 1][j];
                                score[i][j] = score[i - 1][j];
                                path[i][j] = 1;


                            }

                            else
                            {
                                max = score[i][j - 1] - (penalty * previousGap2);
                                score[i][j] = score[i][j - 1] - (penalty * previousGap2);
                                path[i][j] = 2;
                                previousGap2++;
                                previousGap1 = 1;
                            }

                            if (score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10) > max)
                            {
                                max = score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25)  + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10);
                                score[i][j] = score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10);
                                path[i][j] = 3;
                                previousGap1 = 1;
                                previousGap2 = 1;

                            }


                        }
                        else
                        {
                            if (score[i - 1][j] - (penalty * previousGap1) > score[i][j - 1] - (penalty * previousGap2))
                            {
                                max = score[i - 1][j] - (penalty * previousGap1);
                                score[i][j] = score[i - 1][j] - (penalty * previousGap1);
                                path[i][j] = 1;
                                previousGap1++;
                                previousGap2 = 1;

                            }

                            else
                            {
                                max = score[i][j - 1] - (penalty * previousGap2);
                                score[i][j] = score[i][j - 1] - (penalty * previousGap2);
                                path[i][j] = 2;
                                previousGap2++;
                                previousGap1 = 1;
                            }

                            if (score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10) > max)
                            {
                                max = score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25)  + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10);
                                score[i][j] = score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10);
                                path[i][j] = 3;
                                previousGap1 = 1;
                                previousGap2 = 1;

                            }

                        }
                    }

                int maxScore =(gestureSize - 1) * 35;
              
                textBox5.Text += "Point[" + ((int)importantPoints[dataBaseIndex][pointNum]).ToString() + "] = " + (maxScore - score[userSize][gestureSize]).ToString() + " ";
                if (maxScore - score[userSize][gestureSize] <= range[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex]+200)
                    maxCounter++;
                
                
            }
            if (maxCounter >= numberOfPoints * 0.8)
                return 1;   // To EDIT
            else return 0;
        }

        // SORRY guys I know I'm doing this so dirty but I really don't have time!
        // This function aligns the Upper Boundary with the Basic Gesture and also the Lower Boundary with the Basic Gesture
        // The returning value is a Range of Alignment Scores(for each point) that a gesture should gain to be valid
        double align2(int dataBaseIndex, int endFrame, int startFrame)
        {
            
            int numberOfPoints = importantPoints[dataBaseIndex].Count;
            for (int pointNum = 0; pointNum != importantPoints[dataBaseIndex].Count; pointNum++)
            {

                score[0][0] = 0;

                for (int i = 1; i != recordedVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Count+1; i++)
                {
                    gestureCode_Direction[i] = (double)(recordedVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex])[i - 1];
                    
                }
                int gestureSize = recordedVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Count;

                
                for (int i = startFrame + 1; i != (endFrame - 15) + 1+1; i++)
                {
                    userCode_Direction[(i - startFrame)] = (double)(boundaryRecordedVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex])[i - 1]; //indexing starts from 1 (0 is sentinel)
                    
                }
                int userSize = (endFrame - 15+1) - (startFrame + 1)+1;

                
                for (int i = 1; i != recordedAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Count+1; i++)
                {
                    gestureCodePureAngle[i] = (double)(recordedAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex])[i - 1];
                    
                }
                int gestureAngleSize = recordedAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Count;

                
                for (int i = startFrame + 1; i != endFrame + 1+1; i++)
                {
                    userCodePureAngle[(i - startFrame)] = (double)(boundaryRecordedAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex])[i - 1];
                    
                }

                for (int j = 0; j != gestureCode_Direction.Length; j++) score[0][j] = 0;
                for (int j = 0; j != userCode_Direction.Length; j++) score[j][0] = 0;

                int previousGap1 = 1;
                int previousGap2 = 1;
                
                for (int i = 0; i != 5000; i++)
                {
                    path[0][i] = 2;
                    path[i][0] = 1;
                }

                for (int j = 1; j != gestureSize + 1; j++)
                    for (int i = 1; i != userSize + 1; i++)
                    {

                        double max = -50000;

                        if (j == gestureSize)
                        {
                            if (score[i - 1][j] > score[i][j - 1] - (penalty * previousGap2))
                            {
                                max = score[i - 1][j];
                                score[i][j] = score[i - 1][j];
                                path[i][j] = 1;


                            }

                            else
                            {
                                max = score[i][j - 1] - (penalty * previousGap2);
                                score[i][j] = score[i][j - 1] - (penalty * previousGap2);
                                path[i][j] = 2;
                                previousGap2++;
                                previousGap1 = 1;
                            }

                            if (score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10) > max)
                            {
                                max = score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10);
                                score[i][j] = score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10);
                                path[i][j] = 3;
                                previousGap1 = 1;
                                previousGap2 = 1;

                            }


                        }
                        else
                        {
                            if (score[i - 1][j] - (penalty * previousGap1) > score[i][j - 1] - (penalty * previousGap2))
                            {
                                max = score[i - 1][j] - (penalty * previousGap1);
                                score[i][j] = score[i - 1][j] - (penalty * previousGap1);
                                path[i][j] = 1;
                                previousGap1++;
                                previousGap2 = 1;

                            }

                            else
                            {
                                max = score[i][j - 1] - (penalty * previousGap2);
                                score[i][j] = score[i][j - 1] - (penalty * previousGap2);
                                path[i][j] = 2;
                                previousGap2++;
                                previousGap1 = 1;
                            }

                            if (score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10) > max)
                            {
                                max = score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10);
                                score[i][j] = score[i - 1][j - 1] + distance(userCode_Direction[i] - gestureCode_Direction[j], 25) + distance(userCodePureAngle[i] - gestureCodePureAngle[j], 10);
                                path[i][j] = 3;
                                previousGap1 = 1;
                                previousGap2 = 1;

                            }

                        }
                    }

                int maxScore = (gestureSize-1)*35;
               
                double difference = maxScore - score[userSize][gestureSize];
                if (difference > range[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex])
                    range[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex] = difference;
                boundaryRecordedVectorAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Clear();
                boundaryRecordedAngle[(int)importantPoints[dataBaseIndex][pointNum]][dataBaseIndex].Clear();

            }
           return 0;
        }

        double distance(double x1, double x2)
        {
            // Instead of just calculating the difference between two vectors or two angles
            // we calculate a distance which is a function of the difference.
            // So if the difference was less than 10 the distance is 25(you can change)for vectors and 10 for angles and so on
            double x = Math.Abs(x1);
            double score = 0;
            if (x <= 10) score = x2;
            else if (x <= 30) score = x2 + 10 - 0.4 * x;
            else score = x2 + 10 - x;

            return score;

        }
        
        private void depth_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            // Record Number
            gestureInfo += "N "+(dataBaseCounter-1).ToString() ; // Showing start of Important Points
            // Save Important Points
            gestureInfo += "\r\nP"; // Showing start of Important Points
            for (int i = 0; i!=importantPoints[dataBaseCounter - 1].Count; i++)
            {
                gestureInfo += importantPoints[dataBaseCounter - 1][i].ToString()+ " ";

            }
            // Save Range for each of Important Points
            gestureInfo += "\r\nR"; // Showing start of Range
            for (int i = 0; i != importantPoints[dataBaseCounter - 1].Count; i++)
            {
                gestureInfo += range[(int)importantPoints[dataBaseCounter - 1][i]][dataBaseCounter-1].ToString()+ " ";

            }
            //Save Angles
            gestureInfo += "\r\n@"; // Showing start of Angles
            for (int P = 0; P != importantPoints[dataBaseCounter - 1].Count; P++)
            {
                for (int i = 0; i != recordedAngle[(int)importantPoints[dataBaseCounter - 1][P]][dataBaseCounter - 1].Count; i++)
                    gestureInfo += recordedAngle[(int)importantPoints[dataBaseCounter - 1][P]][dataBaseCounter - 1][i].ToString() + " ";
                gestureInfo += "End ";
            }
            //Save Vector Angles
            gestureInfo += "\r\nV";// Showing start of Vectors
            for (int P = 0; P != importantPoints[dataBaseCounter - 1].Count; P++)
            {
                for (int i = 0; i != recordedVectorAngle[(int)importantPoints[dataBaseCounter - 1][P]][dataBaseCounter - 1].Count; i++)
                    gestureInfo += recordedVectorAngle[(int)importantPoints[dataBaseCounter - 1][P]][dataBaseCounter - 1][i].ToString() + " ";
                gestureInfo += "End ";
            }

            string fileName = "Gesture" + (dataBaseCounter-1).ToString()+ participantNum.Text + ".txt";
            System.IO.File.WriteAllText(@"C:\HCI Lab\" + fileName,gestureInfo);
            status.Text = "Saved to " + fileName;

        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text documents (.txt)|*.txt";

            dlg.InitialDirectory = @"C:\HCI Lab\";

            // Display OpenFileDialog by calling ShowDialog method
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == true)
            {
                // Open document
                LoadGesturesFromFile(dlg.FileName);
                //dtwTextOutput.Text = _dtw.RetrieveText();
                status.Text = "Gestures loaded!";
            }
        }
        public void LoadGesturesFromFile(string fileLocation)
        {
            int itemCount = 0;
            string[] line=new string[20];
            string block;
            string gestureName = String.Empty;
            
            ArrayList frames = new ArrayList();
            double[] items = new double[12];
            int recordNumber=0;
            int[] loadedImportantPoints=new int[20];
            int numberOfImportantPoints = 0;
            // Read the file and display it line by line.
            System.IO.StreamReader file = new System.IO.StreamReader(fileLocation);
            block = file.ReadToEnd();
            
            line[0] = block.Split('P')[0]; //N block
            block = block.Split('P')[1];
            line[1] = block.Split('R')[0]; // P block
            int[] lineSize=new int[20];
            block = block.Split('R')[1];
            line[2] = block.Split('@')[0]; // R block
            block = block.Split('@')[1];
            line[3] = block.Split('V')[0]; // @ block
            block = block.Split('V')[1];
            line[4] = block;// V block
               

            recordNumber = Convert.ToInt32(line[0].Split(' ')[1]);
           

            //initialization
            for (int point = 0; point != 20; point++)
            {
                recordedAngle[point][recordNumber] = new ArrayList();
                recordedVectorAngle[point][recordNumber] = new ArrayList();
                boundaryRecordedAngle[point][recordNumber] = new ArrayList();
                boundaryRecordedVectorAngle[point][recordNumber] = new ArrayList();
                range[point][recordNumber] = 0;
                
            }
            dataBaseCounter++;
                    
               
            char[] space = new char[1];
            // space[0]=' ';

            
            // line[1] = line[1].Remove(lineSize[1]-2,2);
           // line[1] = line[1].Remove(0, 2); // Remove "P "   
            lineSize[1] = line[1].Split(' ').Count() - 2;
            for (int i = 0; i != lineSize[1]+1; i++)
            {
                loadedImportantPoints[i] = Convert.ToInt32(line[1].Split(' ')[i]);
                importantPoints[recordNumber].Add(loadedImportantPoints[i]);
                numberOfImportantPoints++;
            }

            
            //line[3] = line[3].Remove(0, 2); // Remove "@ "
            string[] angles = line[3].Split(' ');
            int nextIndex = 0;
            for (int i = 0;i!=numberOfImportantPoints; i++)
            {
                for (int j = nextIndex; angles[j] != "End"; j++) //2 for spaces
                {
                    recordedAngle[loadedImportantPoints[i]][recordNumber].Add(Convert.ToDouble(angles[j]));
                    nextIndex = j + 2;
                }
            }

            //line[2] = line[2].Remove(0, 2);
            for (int i = 0; i != numberOfImportantPoints; i++)
            {
                range[loadedImportantPoints[i]][recordNumber] = Convert.ToDouble(line[2].Split(' ')[i]);
            }
                    

            itemCount++;
               
           // line[4] = line[4].Remove(0, 2); // Remove "@ "
            string[] vectorAngles = line[4].Split(' ');
            nextIndex = 0;
            for (int i = 0; i != numberOfImportantPoints; i++)
            {
                for (int j = nextIndex; vectorAngles[j] != "End"; j++) //2 for spaces
                {
                    recordedVectorAngle[loadedImportantPoints[i]][recordNumber].Add(Convert.ToDouble(vectorAngles[j]));
                    nextIndex = j + 2; //if we are already at the end,next step is "j+1" so next number is "j+2"
                }
                
            }

            
            file.Close();
        }

        private void button3_Click(object sender, RoutedEventArgs e)
        {
            wait(40000000);
            liveCapturing = true;
            status.Text = "LIVE!";
          
        }

      
    }
}

