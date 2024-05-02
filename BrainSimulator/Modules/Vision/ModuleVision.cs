﻿//
// PROPRIETARY AND CONFIDENTIAL
// Brain Simulator 3 v.1.0
// © 2022 FutureAI, Inc., all rights reserved
//

using BrainSimulator.Modules.Vision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using System.Diagnostics;
using static System.Math;
using UKS;

namespace BrainSimulator.Modules
{
    public class ModuleVision : ModuleBase
    {
        //any public variable you create here will automatically be saved and restored  with the network
        //unless you precede it with the [XmlIgnore] directive
        public string currentFilePath = "";
        public string previousFilePath = "";
        [XmlIgnore]
        public BitmapImage bitmap = null;
        [XmlIgnore]
        public float[,] boundaryArray;
        [XmlIgnore]
        public List<Corner> corners;
        [XmlIgnore]
        public List<Segment> segments;
        [XmlIgnore]
        public HSLColor[,] imageArray;
        [XmlIgnore]
        public HoughTransform segmentFinder;
        public class Corner
        {
            public PointPlus location;
            public Angle angle;
            public Angle orientation;
            public override string ToString()
            {
                return $" ({(int)Round(location.X)},{(int)Round(location.Y)}) A: {angle}";// O: {orientation}";
            }
        }

        public ModuleVision()
        {
        }

        //fill this method in with code which will execute
        //once for each cycle of the engine
        public override void Fire()
        {
            Init();  //be sure to leave this here

            if (currentFilePath == previousFilePath) return;
            previousFilePath = currentFilePath;

            bitmap = new BitmapImage();

            try
            {
                // Initialize the bitmap with the URI of the file
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(currentFilePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // To load the image fully at load time
                bitmap.EndInit();
                bitmap.Freeze(); // Make the BitmapImage thread safe
            }
            catch (Exception ex)
            {
                // Handle exceptions such as file not found or invalid file format
                Debug.WriteLine($"An error occurred: {ex.Message}");
                return;
            }


            if (bitmap.Width > 200 || bitmap.Height > 200)
            {
                Debug.WriteLine($"Image too large for current implementation");
                return;
            }


            HSLColor[,] imageArray;
            imageArray = GetImageArrayFromBitmapImage();
            boundaryArray = FindBoundaries(imageArray);

            segmentFinder = new(boundaryArray.GetLength(0), boundaryArray.GetLength(1));
            segmentFinder.Transform(boundaryArray);

            segments = segmentFinder.ExtractLineSegments();

            FindCorners(segments);

            FindOrphanSegmentEnds();

            FindOutlines();

            UpdateDialog();

        }

        private HSLColor[,] GetImageArrayFromBitmapImage()
        {
            imageArray = new HSLColor[(int)bitmap.Width, (int)bitmap.Height];
            int stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8;
            byte[] pixelBuffer = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixelBuffer, stride, 0);
            for (int i = 0; i < imageArray.GetLength(0); i++)
            {
                for (int j = 0; j < imageArray.GetLength(1); j++)
                {
                    //upper for jpeg, lower for png
                    int index = j * stride + i * 4; // Assuming 32 bits per pixel (4 bytes: BGRA)
                    if (bitmap.Format.BitsPerPixel == 8)
                        index = j * stride * 3 + i * 3;

                    if (bitmap.Format.BitsPerPixel != 8 && index < pixelBuffer.Length - 3)
                    {
                        byte blue = pixelBuffer[index];
                        byte green = pixelBuffer[index + 1];
                        byte red = pixelBuffer[index + 2];
                        byte alpha = pixelBuffer[index + 3];
                        HSLColor pixelColor = new HSLColor(1, red, green, blue);
                        imageArray[i, j] = pixelColor;
                    }
                    else
                    {
                        var c = bitmap.Palette.Colors[pixelBuffer[index]];
                        byte blue = c.B;
                        byte red = c.R;
                        byte green = c.G;
                        HSLColor pixelColor = new HSLColor(1, red, green, blue);
                        imageArray[i, j] = pixelColor;

                    }
                }
            }

            return imageArray;
        }

        private float[,] FindBoundaries(HSLColor[,] imageArray)
        {
            float[,] boundaryArray = new float[imageArray.GetLength(0), imageArray.GetLength(1)];

            float dx = 1;
            float dy = 0;
            int sx = 0; int sy = 0;
            for (sy = 0; sy < imageArray.GetLength(1); sy++)
            {
                int[] bRay = new int[imageArray.GetLength(0)];
                var rayThruImage = LineThroughArray(dx, dy, sx, sy, imageArray);
                FindBoundariesInRay(bRay, rayThruImage);
                for (int i = 0; i < bRay.GetLength(0); i++)
                {
                    boundaryArray[i, sy] = bRay[i];
                }
            }
            dx = 0;
            dy = 1;
            sy = 0;
            for (sx = 0; sx < imageArray.GetLength(1); sx++)
            {
                int[] bRay = new int[imageArray.GetLength(1)];
                var rayThruImage = LineThroughArray(dx, dy, sx, sy, imageArray);
                FindBoundariesInRay(bRay, rayThruImage);
                for (int i = 0; i < bRay.GetLength(0); i++)
                {
                    if (boundaryArray[sx, i] != 1)
                        boundaryArray[sx, i] = bRay[i];
                }
            }
            return boundaryArray;
        }

        private void FindBoundariesInRay(int[] bRay, List<HSLColor> rayThruImage)
        {
            //todo: filter out noisy areas in the ray
            //todo: handle boundaries which fade over a few pixels
            for (int i = 0; i < rayThruImage.Count - 1; i++)
            {
                float diff = PixelDifference(rayThruImage[i], rayThruImage[i + 1]);
                if (diff > 0.4)
                    bRay[i] = 1;
            }
            //combine/remove nearby boundary points
            for (int i = 0; i < bRay.GetLength(0) - 3; i++)
            {
                if (bRay[i] == 1 && bRay[i + 1] == 1)
                    bRay[i] = 0;
                else if (bRay[i] == 1 && bRay[i + 2] == 1)
                {
                    bRay[i] = 0;
                    bRay[i + 1] = 1;
                    bRay[i + 2] = 0;
                }
            }
        }

        List<HSLColor> LineThroughArray(float dx, float dy, int startX, int startY, HSLColor[,] imageArray)
        {
            List<HSLColor> retVal = new();
            float x = startX; float y = startY;

            while (x < imageArray.GetLength(0) && y < imageArray.GetLength(1))
            {
                HSLColor c = imageArray[(int)x, (int)y];
                if (c != null)
                    retVal.Add(c);
                x += dx;
                y += dy;
            }
            return retVal;
        }

        float PixelDifference(HSLColor c1, HSLColor c2)
        {
            float hueWeight = .2f;
            float satWeight = .2f;
            float lumWeight = 1.0f;
            float retVal = Abs(c1.hue - c2.hue) * hueWeight + Abs(c1.saturation - c2.saturation) * satWeight +
                Abs(c1.luminance - c2.luminance) * lumWeight;
            return retVal;
        }

        private void FindCorners(List<Segment> segmentsIn)
        {
            //first lengthen the segments
            List<Segment> segments = new List<Segment>();
            foreach (Segment s in segmentsIn)
                segments.Add(new Segment(s.P1, s.P2));
            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                //try to extend each end of the segment until it runs out of boundary pixels
                while (boundaryArray[(int)Round(segment.P1.X), (int)Round(segment.P1.Y)] > .1)
                {
                    PointPlus p = Utils.ExtendSegment(segment.P1, segment.P2, 1, true);
                    if (p.X < 0 || p.Y < 0 || Round(p.X) >= boundaryArray.GetLength(0) || Round(p.Y) >= boundaryArray.GetLength(1)) break;
                    if (boundaryArray[(int)Round(p.X), (int)Round(p.Y)] > .1)
                        segment.P1 = p;
                    else
                        break;
                }
                while (boundaryArray[(int)Round(segment.P2.X), (int)Round(segment.P2.Y)] > .1)
                {
                    PointPlus p = Utils.ExtendSegment(segment.P1, segment.P2, 1, false);
                    if (p.X < 0 || p.Y < 0 || Round(p.X) >= boundaryArray.GetLength(0) || Round(p.Y) >= boundaryArray.GetLength(1)) break;
                    if (boundaryArray[(int)Round(p.X), (int)Round(p.Y)] > .1)
                        segment.P2 = p;
                    else
                        break;
                }
            }
            for (int i = 0; i < segments.Count; i++)
            {
                //now extend another 3 or more pixels, just for good measure
                float distToExtend = (float)Math.Max(3, segments[i].Length * 0.10);
                segments[i] = Utils.ExtendSegment(segments[i], distToExtend);
            }

            //Now, find the corners
            corners = new List<Corner>();

            segments = segments.OrderByDescending(x => x.Length).ToList();
            corners = new();
            for (int i = 0; i < segments.Count; i++)
            {
                Segment s1 = segments[i];
                for (int j = i; j < segments.Count; j++)
                {
                    Segment s2 = segments[j];
                    if (Utils.FindIntersection(s1, s2, out PointPlus intersection, out Angle angle))
                    {
                        //ignore angles whih are nearly straight
                        //if the segments are short, the angle between them must be larger
                        Angle minAngle = new Angle();
                        minAngle.Degrees = 10;
                        if (s1.Length < 20 || s2.Length < 20) minAngle.Degrees = 20;


                        if (Abs(angle.Degrees) > minAngle.Degrees && 180 - Abs(angle.Degrees) > minAngle.Degrees)
                        {
                            Angle cornerOrientation = ((s1.Angle - s2.Angle) > 0) ? s1.Angle : s2.Angle + (s1.Angle + s2.Angle) / 2;

                            Corner alreadyInList = corners.FindFirst(x =>
                                Abs(x.location.X - intersection.X) < 4 &&
                                Abs(x.location.Y - intersection.Y) < 4 &&
                                Abs(x.orientation - cornerOrientation) > 15);
                            if (alreadyInList == null)
                            {
                                corners.Add(new Corner { location = intersection, angle = Abs(angle), orientation = cornerOrientation });
                            }
                        }
                    }
                }
            }
        }

        void FindOrphanSegmentEnds()
        {
            foreach (Segment s in segments)
            {
                bool p1isOrphanEnd = true;
                bool p2isOrphanEnd = true;
                //is either endpoint of the segment near an existing corner?
                //This is because segments may intersect (corner) at their middles
                foreach (Corner c in corners)
                {
                    if ((s.P1 - c.location).R < 5)
                        p1isOrphanEnd = false;
                    if ((s.P2 - c.location).R < 5)
                        p2isOrphanEnd = false;
                }
                //is either endpoint near annother segment?
                foreach (Segment s1 in segments)
                {
                    if (s1 == s) continue;
                    if (s1.Length > 3)
                    {
                        float d1 = Utils.DistancePointToSegment(s1, s.P1);
                        float d2 = Utils.DistancePointToSegment(s1, s.P2);
                        if (d1 < 3)
                            p1isOrphanEnd = false;
                        if (d2 < 3)
                            p2isOrphanEnd = false;
                    }
                }
                if (p1isOrphanEnd)
                    corners.Add(new Corner { location = s.P1, angle = 0 });
                if (p2isOrphanEnd)
                    corners.Add(new Corner { location = s.P2, angle = 0 });
            }
        }

        //trace around an outline to get the order of corners and relative distances
        private void FindOutlines()
        {
            //TODO modify to handle multiple outlines
            List<Corner> outline = new();

            //set up the UKS structure for outlines
            GetUKS();
            if (UKS == null) return;
            UKS.GetOrAddThing("Sense", "Thing");
            UKS.GetOrAddThing("Visual", "Sense");
            Thing outlines = UKS.GetOrAddThing("Outline", "Visual");
            Thing tCorners = UKS.GetOrAddThing("Corners", "Visual");
            UKS.DeleteAllChildren(outlines);
            UKS.DeleteAllChildren(tCorners);

            if (corners.Count == 0) return;

            //for convenience in debugging
            corners = corners.OrderBy(x => x.location.X).OrderBy(x => x.location.Y).ToList();

            ////here's a list of which corners are connected to which others
            ////this is used to be able to find shared corners easily
            //List<int>[] connectedCorners = new List<int>[corners.Count];
            //for (int i = 0; i < connectedCorners.GetLength(0); i++) connectedCorners[i] = new();

            ////find the next corner which is connected by a boundary which is not already in the outline
            //for (int i = 0; i < corners.Count; i++)
            //{
            //    for (int j = i+1; j < corners.Count; j++)
            //    {
            //        Corner corner1 = corners[i];
            //        Corner corner2 = corners[j];
            //        float dist = (corner1.location - corner2.location).R;
            //        float weight = segmentFinder.SegmentWeight(
            //            new Segment { P1 = corner1.location, P2 = corner2.location, });
            //        if (weight > dist / 3)
            //        {
            //            if (!connectedCorners[i].Contains(j))
            //                connectedCorners[i].Add(j);
            //            if (!connectedCorners[j].Contains(i))
            //                connectedCorners[j].Add(i);
            //        }
            //        else { }
            //    }
            //}

            for (int i = 0; i < corners.Count; i++)
                outline.Add(corners[i]);

            //make this a right-handed list of points  
            double sum = 0;
            int cnt = outline.Count;
            for (int i = 0; i < outline.Count; i++)
            {
                sum += (outline[i].location.X - outline[(i + 1) % cnt].location.X) *
                    (outline[i].location.Y - outline[(i + 1) % cnt].location.Y);
            }
            if (sum > 0) outline.Reverse();

            //we now have an ordered, right-handed outline
            //let's add it to the UKS

            //TODO: here's where we'll have a loop for multiple outlines
            Thing currOutline = UKS.GetOrAddThing("Outline*", "Outlines");
            foreach (Corner c in outline)
            {
                Thing corner = UKS.GetOrAddThing("corner*", tCorners);
                corner.V = c;
                UKS.AddStatement(currOutline, "has*", corner);
            }
        }



        // fill this method in with code which will execute once
        // when the module is added, when "initialize" is selected from the context menu,
        // or when the engine restart button is pressed
        public override void Initialize()
        {
        }

        // the following can be used to massage public data to be different in the xml file
        // delete if not needed
        public override void SetUpBeforeSave()
        {
        }

        public override void SetUpAfterLoad()
        {
        }

        // called whenever the size of the module rectangle changes
        // for example, you may choose to reinitialize whenever size changes
        // delete if not needed
        public override void SizeChanged()
        {

        }

    }
}