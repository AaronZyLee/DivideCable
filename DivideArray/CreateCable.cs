using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace Cable
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]

    public class CreateCable : IExternalCommand
    {

        Document doc;
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            doc = app.ActiveUIDocument.Document;

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            if (collector != null)
                collector.OfClass(typeof(FamilyInstance));
            IList<Element> list = collector.ToElements();

            //选择工井作为起始点
            Autodesk.Revit.UI.Selection.Selection sel = app.ActiveUIDocument.Selection;
            WellFilter fmft = new WellFilter();
            Reference refWell = sel.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element,fmft,"请选择起始工井");
            FamilyInstance start = doc.GetElement(refWell) as FamilyInstance;

            //将起点位置加入location中
            List<XYZ> location = new List<XYZ>();
            if(start!=null)
                location.Insert(0, (start.Location as LocationPoint).Point);

            //过滤非工井族的类型
            List<string> familyName = new List<string>();
            string[] fn = { "直通工井", "三通工井", "四通工井", "三通工井(6.0×2.5×1.9）", "宽口三通工井(12.0×2.5×1.9）" };
            familyName.AddRange(fn);

            //其余点先加入temp中
            List<XYZ> temp = new List<XYZ>();
            foreach (Element e in list)
            {
                FamilyInstance f = e as FamilyInstance;
                if (familyName.Contains(f.Symbol.Family.Name) && f.Id != start.Id)
                    temp.Add((f.Location as LocationPoint).Point);
            }

            //根据和起点距离的大小，将其余工井的位置按顺序插入location中
            int count = temp.Count;
            for(int i=0;i<count;i++){
                double min = double.PositiveInfinity;
                int index_min = 0;
                for (int j = 0; j < temp.Count; j++)
                {
                    if (temp[j].DistanceTo(location[i]) < min)
                    {
                        min = temp[j].DistanceTo(location[i]);
                        index_min = j;
                    }
                }
                location.Add(temp[index_min]);
                temp.RemoveAt(index_min);
            }

            //获取工井间隔的距离
            List<double> intervals = getIntervals(location);
            List<List<int>> index = DivideBigComponent(intervals);
            List<XYZ> cutPoint = new List<XYZ>();

            if (index != null)
            {
                int sentinel = 0;
                for (int i = 0; i < index.Count; i++)
                {
                    for (int j = 0; j < index[i].Count; j++)
                    {
                        if (j == index[i].Count - 1)
                            sentinel += index[i][j];
                        else
                            cutPoint.Add(location[index[i][j]+sentinel]);
                    }
                }
            }
                
                string result = "";
                for (int i = 0; i < cutPoint.Count; i++)
                {
                    result += cutPoint[i] + " ";
                }
                TaskDialog.Show("1", result);


            
           
             

            //选取项目中的电缆
            FlexDuctFilter fdFilter = new FlexDuctFilter();
            Reference refCable = sel.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element,fdFilter,"请选择需要分割的电缆");
            FlexDuct cable = doc.GetElement(refCable) as FlexDuct;

            Transaction trans = new Transaction(doc);
            trans.Start("分割电缆");
            if (cable != null)
            {
                TaskDialog.Show("1", "ok!");
                cutCableAtPoint(doc, cable, cutPoint);
            }
            trans.Commit();    
            

            return Result.Succeeded;
        }

        #region Divide Cable
        public List<List<int>> DivideBigComponent(List<double> arraylist)
        {
            int count = arraylist.Count;
            double total = 0;
            for (int i = 0; i < arraylist.Count; i++)
                total += arraylist[i];
            List<List<int>> result = new List<List<int>>();

            int step_min = (int)(total / 1800) + 1;
            int step_max = (int)(total / 900);
            int step = step_min;
            bool isSucceeded = false;

            while (!isSucceeded && step <= step_max)
            {
                int position = 0;
                result.Clear();
                for (int i = 0; i < step; i++)
                {
                    List<int> index = null;
                    List<double> subArray = null;
                    if (i == step - 1)
                    {
                        subArray = arraylist.GetRange(position, count - position);
                    }
                    else
                    {
                        if (position + count / step > count)
                            break;
                        subArray = arraylist.GetRange(position, count / step);
                    }
                    index = DivideArray(subArray);

                    int temp = count / step;
                    while (index.Count != 4 && i != step - 1 && temp < count - position)
                    {
                        subArray = arraylist.GetRange(position, ++temp);
                        index = DivideArray(subArray);
                    }
                    result.Add(index);
                    position += temp;

                    if (i == step - 1 && index.Count == 4)
                    {
                        isSucceeded = true;
                    }
                }
                step++;
            }
            if (isSucceeded)
                return result;
            return null;

        }

        public List<int> DivideArray(List<double> arraylist)
        {

            List<double> sum_array = new List<double>();
            List<int> list_index = new List<int>();
            int count = arraylist.Count;

            double range = 600;

            while (!isValidDivision(sum_array) && range >= 300)
            {
                double sum = 0;
                sum_array.Clear();
                list_index.Clear();
                list_index.Add(0);
                for (int i = 0; i < count; i++)
                {
                    sum += arraylist[i];
                    if (sum > range)
                    {
                        list_index.Add(i);
                        sum_array.Add(sum - arraylist[i]);
                        sum = arraylist[i];
                    }
                    if (i == count - 1)
                        sum_array.Add(sum);
                }
                range -= 10;
            }

            list_index.Add(count);

            return list_index;
        }

        public bool isValidDivision(List<double> sum_array)
        {
            if (sum_array.Count != 3)
                return false;

            double max = sum_array[0], min = sum_array[0];
            for (int i = 1; i < sum_array.Count; i++)
            {
                if (sum_array[i] > max)
                    max = sum_array[i];
                if (sum_array[i] < min)
                    min = sum_array[i];
            }

            if (min * 1.3 < max)
                return false;
            return true;
        }
        #endregion

        #region 使用点列表来创建软管的方法
        /// <summary>
        /// 使用点列表来创建软管
        /// </summary>
        /// <param name="document"></param>
        /// <param name="points">点列表</param>
        /// <param name="radius">半径</param>
        public FlexDuct CreateFlexDuct(Document document, List<XYZ> points, double radius)
        {
            // find a pipe type
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FlexDuctType));
            FlexDuctType flexDuctType = null;
            MEPSystemType mepsystemtype = null;
            foreach (FlexDuctType type in collector)
            {
                if (type.Name.Contains("电缆"))
                {
                    flexDuctType = type;
                }
            }
            #region GetMEPSystemTypeName
            var MepSystemFilter = new ElementClassFilter(typeof(MEPSystemType));
            FilteredElementCollector MepSystemTypes = new FilteredElementCollector(document);
            var MepSystemResult = MepSystemTypes.WherePasses(MepSystemFilter);
            foreach (MEPSystemType Mepsystemtype in MepSystemResult)
            {

                mepsystemtype = Mepsystemtype;
                if (mepsystemtype != null)
                {
                    break;
                }
            }
            #endregion
            #region 找参照标高
            Level level = null;
            FilteredElementIdIterator fei = new FilteredElementCollector(document).OfClass(typeof(Level)).GetElementIdIterator();
            fei.Reset();
            while (fei.MoveNext())
            {
                level = document.GetElement(fei.Current) as Level;
                break;
            }
            #endregion

            FlexDuct newflexduct = null;
            if (flexDuctType != null && mepsystemtype != null && points.Count>=2)
            {
                XYZ starttangent = points[1] - points[0];
                XYZ endtangent = points[points.Count - 1] - points[points.Count - 2];
                newflexduct = FlexDuct.Create(document, mepsystemtype.Id, flexDuctType.Id, level.Id, starttangent, endtangent, points);
                newflexduct.LookupParameter("直径").SetValueString(radius.ToString());
            }

            return newflexduct;

        }
        #endregion

        /// <summary>
        /// 创建非开挖排管
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="unpipe">非开挖排管</param>
        /// <param name="vector">方向向量</param>
        public void CreateUndigPipeCable(Document doc, FamilyInstance unpipe, XYZ vector)
        {
            LocationCurve lc = unpipe.Location as LocationCurve;
            Curve c = lc.Curve;
            XYZ startpoint = c.GetEndPoint(0);
            XYZ endpoint = c.GetEndPoint(1);
            double lenth = Distance(startpoint, endpoint);
            XYZ direction = endpoint - startpoint;
            direction = direction / lenth;
            List<XYZ> points = new List<XYZ>();

            double endlengh = 2200;

            double R = lenth - MmtoFt(endlengh * 2);
            XYZ arcstartpoint = startpoint + direction * (MmtoFt(endlengh));

            XYZ mid = midPoint(startpoint, endpoint);
            double xc = Distance(XYZ.Zero, arcstartpoint + R / 2 * direction);
            double yc = mid.Z + R / 2 * Math.Sqrt(3);
            int n = 5;
            int n2 = 20;


            for (int i = 0; i < n; i++)
            {
                points.Add(startpoint + MmtoFt(endlengh) / n * i * direction + vector);
            }

            for (int i = 1; i < n2; i++)
            {
                XYZ p1 = arcstartpoint + R / n2 * i * direction;
                double x1 = R / n2 * i - R / 2;
                double y1 = yc - Math.Sqrt(R * R - (x1) * (x1));
                double x = R / n2 * i * direction.X + arcstartpoint.X;
                double y = R / n2 * i * direction.Y + arcstartpoint.Y;
                double z = y1;
                points.Add(new XYZ(x + vector.X, y + vector.Y, z + vector.Z));
            }
            for (int i = n - 1; i >= 0; i--)
            {
                points.Add(endpoint - MmtoFt(endlengh) / n * i * direction + vector);
            }


            CreateFlexDuct(doc, points, 100);
        }

        /// <summary>
        /// 连接两段电缆
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="cable1"></param>
        /// <param name="cable2"></param>
        public void connectCable(Document doc, FlexDuct cable1, FlexDuct cable2)
        {

            MEPSystemType mepsystemtype = null;
            #region GetMEPSystemTypeName
            var MepSystemFilter = new ElementClassFilter(typeof(MEPSystemType));
            FilteredElementCollector MepSystemTypes = new FilteredElementCollector(doc);
            var MepSystemResult = MepSystemTypes.WherePasses(MepSystemFilter);
            foreach (MEPSystemType Mepsystemtype in MepSystemResult)
            {

                mepsystemtype = Mepsystemtype;
                if (mepsystemtype != null)
                    break;
            }
            #endregion

            #region 找参照标高
            Level level = null;
            FilteredElementIdIterator i = new FilteredElementCollector(doc).OfClass(typeof(Level)).GetElementIdIterator();
            i.Reset();
            while (i.MoveNext())
            {
                level = doc.GetElement(i.Current) as Level;
                break;
            }
            #endregion



            List<XYZ> points1 = new List<XYZ>();
            points1.Add(cable1.Points[0]);
            points1.Add(cable1.Points[cable1.Points.Count - 1]);

            List<XYZ> points2 = new List<XYZ>();
            points2.Add(cable2.Points[0]);
            points2.Add(cable2.Points[cable2.Points.Count - 1]);

            List<XYZ> connPoints = getNearestPoint(points1, points2);
            List<XYZ> zhenghe = new List<XYZ>();
            XYZ startpoint = null;
            if (Distance(connPoints[0], cable1.Points[cable1.Points.Count - 1]) < 0.1)
                startpoint = connPoints[0];
            if (Distance(connPoints[0], cable1.Points[0]) < 0.1)
                startpoint = cable1.Points[cable1.Points.Count - 1];

            string d = cable1.LookupParameter("直径").AsValueString();
            mergeCable(startpoint, cable1, zhenghe);
            mergeCable(zhenghe[zhenghe.Count - 1], cable2, zhenghe);

            XYZ d1 = getDirection(startpoint, cable1.Points.ToList());
            XYZ d2 = getDirection(zhenghe[zhenghe.Count - 1], zhenghe);

            FlexDuct zhenghefd = FlexDuct.Create(doc, mepsystemtype.Id, cable1.FlexDuctType.Id, level.Id, -d1, -d2, zhenghe);
            zhenghefd.LookupParameter("直径").SetValueString(d);
            doc.Delete(cable2.Id);
            doc.Delete(cable1.Id);
        }



        /// <summary>
        /// 将电缆从point点切断
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="cable"></param>
        /// <param name="point">切断点（不一定在电缆上）</param>
        public FlexDuct cutCableAtPoint(Document doc, FlexDuct cable, XYZ point)
        {
            double dis = double.PositiveInfinity;
            IList<XYZ> cablepoints = cable.Points;
            List<XYZ> firstLine = new List<XYZ>();
            List<XYZ> secondLine = new List<XYZ>();

            for (int i = 0; i < cablepoints.Count; i++)
            {
                if (Distance(point, cablepoints[i]) <= dis)
                {
                    firstLine.Add(cablepoints[i]);
                    dis = Distance(point, cablepoints[i]);
                }
                else
                { secondLine.Add(cablepoints[i]); }
            }
            if (firstLine.Count != 0 && secondLine.Count != 0)
            {
                string radius = cable.LookupParameter("直径").AsValueString();
                double r = 100;
                radius = Regex.Replace(radius, "[a-z]", "", RegexOptions.IgnoreCase);
                double.TryParse(radius, out r);
                CreateFlexDuct(doc, firstLine, r);
                FlexDuct secondline = CreateFlexDuct(doc, secondLine, r);
                doc.Delete(cable.Id);

                return secondline;
            }
            return null;
        }

        public void cutCableAtPoint(Document doc, FlexDuct cable, List<XYZ> location)
        {
            FlexDuct start = null;
            int i = 0;
            start = cutCableAtPoint(doc, cable, location[0]);
            while (start == null && i<location.Count-1)
            {
                i++;
                start = cutCableAtPoint(doc, cable, location[i]);
            }
            if(start !=null)
                while (i < location.Count-1)
                {
                    i++;
                    start = cutCableAtPoint(doc, start, location[i]);
                }
            
        }

        /// <summary>
        /// 合并电缆
        /// </summary>
        /// <param name="startpoint">起始点</param>
        /// <param name="fd">添加进列表的电缆</param>
        /// <param name="points">合并点列表</param>
        public void mergeCable(XYZ startpoint, FlexDuct fd, List<XYZ> points)
        {
            List<XYZ> fdpoints = fd.Points.ToList();
            if (Distance(fdpoints[0], startpoint) < Distance(fdpoints[fdpoints.Count - 1], startpoint))
                for (int i = 0; i < fdpoints.Count; i++)
                    points.Add(fdpoints[i]);
            else
                for (int i = fdpoints.Count - 1; i >= 0; i--)
                    points.Add(fdpoints[i]);
        }

        

        #region Helper Function
        private double Distance(XYZ p1, XYZ p2)
        {
            return Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y) + (p1.Z - p2.Z) * (p1.Z - p2.Z));
        }

        private double MtoFt(double m)
        { return (m * 3.28); }
        private double MmtoFt(double mm)
        { return (mm / 1000 * 3.28); }
        private double FttoM(double ft)
        { return (ft * 0.3048); }

        private XYZ midPoint(XYZ p1, XYZ p2)
        { return (new XYZ((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, (p1.Z + p2.Z) / 2)); }

        private List<double> getIntervals(List<XYZ> myLocation)
        {
            List<double> intervals = new List<double>();
            for (int i = 0; i < myLocation.Count - 1; i++)
                intervals.Add(FttoM(myLocation[i].DistanceTo(myLocation[i + 1])));
            return intervals;
        }

        public XYZ getDirection(XYZ point, List<XYZ> line)
        {
            int n = 1;
            XYZ direction = new XYZ();
            XYZ nextPoint = new XYZ();
            for (int i = 0; i < line.Count; i++)
            {
                if (point.DistanceTo(line[i]) < 0.01)
                {
                    n = i;
                    break;
                }
            }
            if (n == 0)
            {
                nextPoint = line[1];
                direction = point - nextPoint;

            }
            else if (n == line.Count - 1)
            {
                nextPoint = line[line.Count - 2];
                direction = point - nextPoint;

            }
            else { direction = null; }
            return direction;
        }

        private List<XYZ> getNearestPoint(List<XYZ> points1, List<XYZ> points2)
        {
            double smallDis = 1000000;
            XYZ finalp1 = new XYZ();
            XYZ finalp2 = new XYZ();
            List<XYZ> result = new List<XYZ>();
            for (int j = 0; j < points1.Count; j++)
                for (int i = 0; i < points2.Count; i++)
                    if (Distance(points1[j], points2[i]) < smallDis)
                    {
                        finalp1 = points1[j];
                        finalp2 = points2[i];
                        smallDis = Distance(points1[j], points2[i]);
                    }
            result.Add(finalp1); result.Add(finalp2);
            return result;
        }
        #endregion
    }

    public class WellFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
    {

        public bool AllowElement(Element elem)
        {
            List<string> familyName = new List<string>();
            string[] fn = { "直通工井", "三通工井", "四通工井", "三通工井(6.0×2.5×1.9）", "宽口三通工井(12.0×2.5×1.9）" };
            familyName.AddRange(fn);

            if (elem is FamilyInstance)
            {
                FamilyInstance f = elem as FamilyInstance;
                return familyName.Contains(f.Symbol.Family.Name);
            }
            return false;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }

    public class FlexDuctFilter : Autodesk.Revit.UI.Selection.ISelectionFilter {
        public bool AllowElement(Element elem)
        {
            return elem is FlexDuct;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
