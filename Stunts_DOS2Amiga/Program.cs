using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResConvert
{
    class Resource3D
    {
        public string Name = "";
        public byte NumPaintJobs;
        public byte Reserved;

        public List<Resource3DVector> Vectors = new List<Resource3DVector>();
        public List<uint> CullFront = new List<uint>();
        public List<uint> CullBack = new List<uint>();
        public List<Resource3DPrimitive> Primitives = new List<Resource3DPrimitive>();

        public uint Offset;

        internal List<byte> ToAmiga()
        {
            //Get the header first
            var result = new List<byte>
            {
                (byte)Vectors.Count,
                (byte)Primitives.Count,
                NumPaintJobs,
                0
            };

            foreach (var vector in Vectors)
            {
                result.AddRange(vector.ToAmiga());
            }
            foreach (var front in CullFront)
            {
                result.AddRange(Program.ToLong(front));
            }
            foreach (var back in CullBack)
            {
                result.AddRange(Program.ToLong(back));
            }
            foreach (var primitive in Primitives)
            {
                result.AddRange(primitive.ToAmiga());
            }

            //Pad to 4
            while (result.Count % 4 != 0)
            {
                result.Add(0);
            }
            return result;
        }
    }

    class Resource3DPrimitive
    {
        public byte type;
        public byte flags;
        public List<byte> materials = new List<byte>();
        public List<byte> indices = new List<byte>();

        internal List<byte> ToAmiga()
        {
            var result = new List<byte>
            {
                type,
                flags
            };
            result.AddRange(materials);
            result.AddRange(indices);
            return result;
        }
    }

    class Resource3DVector
    {
        public short X;
        public short Y;
        public short Z;

        internal List<byte> ToAmiga()
        {
            var result = new List<byte>();
            result.AddRange(Program.ToWord(X));
            result.AddRange(Program.ToWord(Y));
            result.AddRange(Program.ToWord(Z));
            result.AddRange(Program.ToWord(0));
            return result;
        }
    }

    class Car
    {
        public string ID = "";
        public string Name = "";
    }

    class Program
    {
        // https://wiki.stunts.hu/wiki/Car_parameters
        static void Main(string[] args)
        {
            try
            {
                var carsCopied = 0;

                var cars = new List<Car>
                {
                    new Car() { ID = "ansx", Name = "Acura NSX" },
                    new Car() { ID = "audi", Name = "Audi Quattro" },
                    new Car() { ID = "vett", Name = "Corvett ZR1" },
                    new Car() { ID = "fgto", Name = "Ferrari GTO" },
                    new Car() { ID = "jagu", Name = "Jaguar XJR9" },
                    new Car() { ID = "coun", Name = "Lamborghini Countach" },
                    new Car() { ID = "lm02", Name = "Lamborghini LM-002" },
                    new Car() { ID = "lanc", Name = "Lancia Delta" },
                    new Car() { ID = "p962", Name = "Porsche 962" },
                    new Car() { ID = "pc04", Name = "Porsche Carrera 4" },
                    new Car() { ID = "pmin", Name = "Porsche March Indy" }
                };

                foreach (var car in cars.ToArray())
                {
                    if (File.Exists("stda" + car.ID + ".psh") == false)
                    {
                        Console.WriteLine("Warning: Can't find Amiga dashboard for " + car.Name);
                        cars.Remove(car);
                    }
                }

                Console.WriteLine("Make sure to have a backup of everything before starting!");

                foreach (var file in Directory.GetFiles(".", "CAR????.RES"))
                {
                    var carID = Path.GetFileNameWithoutExtension(file).Substring(3, 4).ToLower();
                    var bytes = File.ReadAllBytes(file);
                    if (bytes[0] == 0 && bytes[1] == 0)
                    {
                        //Assume already in Amiga format - continue
                        continue;
                    }

                    var file2 = "st" + carID + ".3sh";
                    if (File.Exists(file2) == false)
                    {
                        //We don't have the 3D model!
                        continue;
                    }


                    Console.WriteLine("Car ID " + carID + " appears to be a DOS Stunts custom car. Do you want to convert to Amiga format? y/n");
                    var key = Console.ReadKey();

                    if (key.KeyChar.ToString().ToLower() != "y")
                    {
                        continue;
                    }

                    //We're converting it!
                    ConvertRES(file);
                    Convert3SH(file2);

                    Console.WriteLine("");
                    Console.WriteLine("We need an Amiga formatted dashboard to add to our custom car. Enter the dashboard to use");
                    var i = 1;
                    foreach (var car in cars)
                    {
                        Console.WriteLine(i + ": " + car.Name);
                        i += 1;
                    }

                    var offset = Console.ReadLine();
                    int.TryParse(offset, out int id);
                    id -= 1;
                    if (id < 0 || id >= cars.Count)
                    {
                        id = 0;
                    }
                    var copiedDashboard = cars[id];
                    File.Copy("stda" + copiedDashboard.ID + ".psh", "stda" + carID + ".psh", true);
                    File.Copy("stdb" + copiedDashboard.ID + ".psh", "stdb" + carID + ".psh", true);

                    Console.WriteLine("Converted " + carID + " to Amiga format");
                    carsCopied++;
                }

                Console.WriteLine("Success! " + carsCopied + " cars converted");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.ReadKey();

        }



        private static void Convert3SH(string fileName)
        {
            var file1 = File.ReadAllBytes(fileName);
            var file2 = new byte[file1.Length];
            Array.Copy(file1, file2, file1.Length);

            //Size
            InvertLong(file1, file2, 0x0);

            //Number of chunks?
            var numOfChunks = InvertInt(file1, file2, 0x4);
            var startOfNames = 6;
            var startOfOffsets = startOfNames + numOfChunks * 4;
            var startOfChunks = startOfOffsets + numOfChunks * 4;

            var resources = new List<Resource3D>();

            for (var i = 0; i < numOfChunks; i++)
            {
                var nameBytes = new byte[4];
                Array.Copy(file1, startOfNames + i * 4, nameBytes, 0, 4);
                var offset = InvertLong(file1, file2, startOfOffsets + i * 4) + startOfChunks;

                var resource3D = new Resource3D
                {
                    Name = Encoding.ASCII.GetString(nameBytes),
                    NumPaintJobs = file1[offset + 2],
                    Reserved = file1[offset + 3],
                };

                var numVertices = file1[offset];
                var numPrimitives = file1[offset + 1];

                offset += 4;

                if (resource3D.Reserved != 0)
                {
                    throw new Exception("Error parsing file!");
                }



                //Read each vertex
                for (var j = 0; j < numVertices; j++)
                {
                    resource3D.Vectors.Add(new Resource3DVector()
                    {
                        X = BitConverter.ToInt16(file1, offset),
                        Y = BitConverter.ToInt16(file1, offset + 2),
                        Z = BitConverter.ToInt16(file1, offset + 4),
                    });
                    offset += 6;
                }

                for (var j = 0; j < numPrimitives; j++)
                {
                    resource3D.CullFront.Add(BitConverter.ToUInt32(file1, offset));
                    offset += 4;
                }
                for (var j = 0; j < numPrimitives; j++)
                {
                    resource3D.CullBack.Add(BitConverter.ToUInt32(file1, offset));
                    offset += 4;
                }
                for (var j = 0; j < numPrimitives; j++)
                {
                    var primitive = new Resource3DPrimitive()
                    {
                        type = file1[offset],
                        flags = file1[offset + 1],
                    };
                    offset += 2;

                    for (var k = 0; k < resource3D.NumPaintJobs; k++)
                    {
                        primitive.materials.Add(file1[offset]);
                        offset += 1;
                    }

                    //How many indices to copy?
                    int indices;
                    if (primitive.type > 0 && primitive.type < 11)
                    {
                        indices = primitive.type;
                    }
                    else if (primitive.type == 11)
                    {
                        indices = 2; //Sphere
                    }
                    else if (primitive.type == 12)
                    {
                        indices = 6; //Wheel
                    }
                    else
                    {
                        indices = 0;
                    }

                    for (var k = 0; k < indices; k++)
                    {
                        primitive.indices.Add(file1[offset]);
                        offset += 1;
                    }

                    resource3D.Primitives.Add(primitive);
                }

                resources.Add(resource3D);

            }

            var resourceBytes = new List<byte>();

            //First pass - Make a big list of all of the bytes
            foreach (var resource in resources)
            {
                resource.Offset = (uint)resourceBytes.Count;
                resourceBytes.AddRange(resource.ToAmiga());
            }

            //Second pass, add each name
            var headerBytes = new List<byte>();
            foreach (var resource in resources)
            {
                headerBytes.AddRange(Encoding.ASCII.GetBytes(resource.Name));
            }

            //Third pass, add the offsets
            foreach (var resource in resources)
            {
                headerBytes.AddRange(ToLong(resource.Offset));
            }

            headerBytes.AddRange(resourceBytes);
            headerBytes.InsertRange(0, ToWord((short)resources.Count));
            headerBytes.InsertRange(0, ToLong((uint)(headerBytes.Count + 4)));
            File.WriteAllBytes(fileName, headerBytes.ToArray());

        }

        internal static byte[] ToLong(uint value)
        {
            var result = BitConverter.GetBytes(value);
            Array.Reverse(result);
            return result;
        }

        internal static byte[] ToWord(short value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            return bytes;
        }

        private static void ConvertRES(string fileName)
        {
            var file1 = File.ReadAllBytes(fileName);
            var file2 = new byte[file1.Length];
            Array.Copy(file1, file2, file1.Length);

            //Size
            InvertLong(file1, file2, 0x0);

            //Number of chunks?
            InvertInt(file1, file2, 0x4);

            //Chunk offsets?
            InvertLong(file1, file2, 0x16);
            InvertLong(file1, file2, 0x1a);
            InvertLong(file1, file2, 0x1e);
            InvertLong(file1, file2, 0x22);

            //Downshift RPM
            InvertInt(file1, file2, 0x2E);

            //Upshift RPM
            InvertInt(file1, file2, 0x30);

            //Maximum RPM
            InvertInt(file1, file2, 0x32);

            //Idle RPM
            InvertInt(file1, file2, 0x2c);

            //Gear ratios
            for (var i = 0; i < 6; i++)
            {
                InvertInt(file1, file2, 0x36 + i * 2);
            }

            //Car mass
            InvertInt(file1, file2, 0x28);

            //Braking effectiveness
            InvertInt(file1, file2, 0x2a);

            //Aerodynamic resistance
            InvertInt(file1, file2, 0x5e);

            //Grip
            InvertInt(file1, file2, 0xca);

            //Surface grip modifiers
            for (var i = 0; i < 4; i++)
            {
                InvertInt(file1, file2, 0xdc + i * 2);
            }

            //Air grip
            InvertInt(file1, file2, 0xda);

            //Wheel coordinates
            for (var i = 0; i < 4 * 3; i++)
            {
                InvertInt(file1, file2, 0xf8 + i * 2);
            }

            //Dimensions for car collisions
            for (var i = 0; i < 4; i++)
            {
                InvertInt(file1, file2, 0xee + i * 2);
            }

            //Shifting knob positions
            for (var i = 0; i < 6 * 2; i++)
            {
                InvertInt(file1, file2, 0x46 + i * 2);
            }

            //Shift pattern centre line
            InvertInt(file1, file2, 0x44);

            //Apparent car height
            InvertInt(file1, file2, 0xf6);

            //Speedometer needle movement
            // (1 pair of integers, 1 integer 
            for (var i = 0; i < 3; i++)
            {
                InvertInt(file1, file2, 0x14e + i * 2);
            }

            //Rev meter needle movement
            //(1 pair of integers, 1 integer 
            for (var i = 0; i < 3; i++)
            {
                InvertInt(file1, file2, 0x224 + i * 2);
            }

            //Other/unknown
            InvertInt(file1, file2, 0x42);
            InvertInt(file1, file2, 0xc8);
            InvertInt(file1, file2, 0xcc);
            InvertInt(file1, file2, 0xce);
            InvertInt(file1, file2, 0xd2);
            InvertInt(file1, file2, 0xd4);
            InvertInt(file1, file2, 0xd6);
            InvertInt(file1, file2, 0xd8);

            InvertInt(file1, file2, 0xe6);
            InvertInt(file1, file2, 0xe8);
            InvertInt(file1, file2, 0xea);
            InvertInt(file1, file2, 0xec);

            File.WriteAllBytes(fileName, file2);
        }

        private static Int32 InvertLong(byte[] file1, byte[] file2, int offset)
        {
            file2[offset + 0] = file1[offset + 3];
            file2[offset + 1] = file1[offset + 2];
            file2[offset + 2] = file1[offset + 1];
            file2[offset + 3] = file1[offset + 0];

            return BitConverter.ToInt32(file1, offset);
        }

        private static Int16 InvertInt(byte[] file1, byte[] file2, int offset)
        {
            file2[offset + 0] = file1[offset + 1];
            file2[offset + 1] = file1[offset + 0];

            return BitConverter.ToInt16(file1, offset);
        }
    }
}

