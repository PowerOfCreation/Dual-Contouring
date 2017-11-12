using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoxelObject : MonoBehaviour
{
    MeshFilter meshFilter;
    Mesh mesh;

    const int sizeX = 2;
    const int sizeY = 2;
    const int sizeZ = 2;

    //All Voxels this object contains
    Voxel[,,] voxels = new Voxel[sizeX, sizeY, sizeZ];

    //All Corners and their locations in a cube
    readonly Vector3[] corners = new Vector3[8] {
        new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,0), new Vector3(0,1,1),
        new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(1,1,0), new Vector3(1,1,1),
    };
    //All 12 edges and their connection to the corners in a cube
    readonly Vector2[] edges = new Vector2[12] {
        new Vector2(0,1), new Vector2(0,2), new Vector2(0,4),
        new Vector2(1,3), new Vector2(1,5),
        new Vector2(2,3), new Vector2(2,6),
        new Vector2(3,7),
        new Vector2(4,5), new Vector2(4,6),
        new Vector2(5,7),
        new Vector2(6,7)
    };

    void Start()
    { 
        //Some Initalization
        mesh = new Mesh();
        meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        InitVoxels();
        RandomDistances();
    }

    void Update()
    {
        //Redraw the stuff when pressing Left Mouse Button
        if (Input.GetButtonDown("Fire1"))
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
            //Not Random at the moment
            RandomDistances();
            //The actual algorithm
            DualContouring();

        }
    }

    public void DualContouring()
    {
        for (int x = 0; x < sizeX - 1; x++)
        {
            for (int y = 0; y < sizeY - 1; y++)
            {
                for (int z = 0; z < sizeZ - 1; z++)
                {
                    //bool to set true/false depending on wether the edges in the current place or inside our outside the shape
                    bool[] inside = new bool[8];

                    //count amount of voxels inside
                    short countOfInsideCorners = 0;

                    for (int i = 0; i < 8; i++)
                    {
                        //Place all corners who are inside the surface in the inside[] array
                        inside[i] = (voxels[
                            (int)(x + corners[i].x),
                            (int)(y + corners[i].y),
                            (int)(z + corners[i].z)].distance <= 0);
                        //Just for "debugging" reasons - place a cube at all inside voxels to make it easier to see in Unity
                        if (inside[i])
                        {
                            countOfInsideCorners++;
                            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            cube.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                            cube.transform.position = new Vector3(x + corners[i].x, y + corners[i].y, z + corners[i].z);
                            cube.transform.SetParent(transform);
                            cube.transform.name = i + "";
                        }
                    }

                    //if all corners are inside OR outside then we don't need to continue/draw any shape
                    if(countOfInsideCorners == 0 || countOfInsideCorners == 8)
                    {
                        continue;
                    }

                    //mark all corners who have a edge which goes "through" the surface - which needs to be drawn
                    bool[] crossingCorners = new bool[8];

                    //find and save all corners which edges perform a sign change(the edge goes through the surface the algorithm is trying to "draw)
                    for (int i = 0; i < 12; ++i)
                    {
                        Vector2 edge = edges[i];
                        if (inside[(int) edge.x] != inside[(int) edge.y])
                        {
                            crossingCorners[(int) edge.x] = true;
                            crossingCorners[(int) edge.y] = true;
                        }
                    }
                    //more or less random array sizes
                    Vector3[] newFace = new Vector3[8];
                    Vector2[] newUV = new Vector2[8];
                    int[] newTriangles = new int[6*3];

                    int faceSize = 0;
                    //Add all Corners who already are part in a Mesh - which still makes it possible they are getting used again but only when a different corners determines them as suitable
                    bool[] processedCorners = new bool[8];

                    for (int i = 0; i < 8; ++i)
                    {
                        //this corner is not suitable/doesn't need to build a mesh
                        if (!crossingCorners[i] || !inside[i] || processedCorners[i])
                        {
                            // Corner has no edge with sign change
                            continue;
                        }
                        //the relative Location of the current corner
                        Vector3 location = corners[i];

                        //some values to start with finding the 2 closest corners
                        float lowestDistance = 10f, secondLowestDistance = 10f;
                        int corner1 = 0, corner2 = 0;
                        //find the the 2 closest corners to form a triangle with
                        for (int corner = 0; corner < 8; ++corner)
                        {
                            if(corner != i && crossingCorners[corner] && inside[corner])
                            {
                                float distance = Vector3.Distance(corners[corner], location);

                                if (distance < lowestDistance)
                                {
                                    //this corner is closer than the currently closest, the currently closest is now the second closest
                                    secondLowestDistance = lowestDistance;
                                    corner2 = corner1;
                                    //and the "new" closest is now the closest
                                    lowestDistance = distance;
                                    corner1 = corner;
                                }
                                else if(distance < secondLowestDistance)
                                {
                                    //this corner is closer than the second closest - so make it the second closest
                                    secondLowestDistance = distance;
                                    corner2 = corner;
                                }
                            }
                        }
                        //form a triangle
                        AddCorner(ref newFace, ref newUV, ref newTriangles, ref faceSize, x, y, z, corner1);
                        AddCorner(ref newFace, ref newUV, ref newTriangles, ref faceSize, x, y, z, corner2);
                        AddCorner(ref newFace, ref newUV, ref newTriangles, ref faceSize, x, y, z, i);
                        //all corners have been used once in a mesh so don't explicitly try again to form a triangle unless a different corners needs corners to form a mesh
                        processedCorners[corner1] = true;
                        processedCorners[corner2] = true;
                        processedCorners[i] = true;

                    }
                    //give the mesh to the Render-Component
                    mesh.vertices = newFace;
                    mesh.uv = newUV;
                    mesh.triangles = newTriangles;
                }
            }
        }
    }

    //Adds a corner to the Array which gets pushed to the GPU
    public void AddCorner(ref Vector3[] newFace, ref Vector2[] newUV, ref int [] newTriangles, ref int faceSize, int x, int y, int z, int corner)
    {
        int xPos = (int)(x + corners[corner].x);
        int yPos = (int)(y + corners[corner].y);
        int zPos = (int)(z + corners[corner].z);

        float distance = voxels[xPos, yPos, zPos].distance;
        Vector3 positonOfCorner = new Vector3(xPos, yPos, zPos) + (distance * (new Vector3(corners[corner].x, corners[corner].y, corners[corner].z) - new Vector3(0.5f, 0.5f, 0.5f)).normalized);

        newFace[faceSize] = positonOfCorner;
        newUV[faceSize] = new Vector2(0, 0);
        newTriangles[faceSize] = faceSize;

        faceSize++;
    }

    //Just "create" the voxel objects in the array
    public void InitVoxels()
    {
        for (int x = 0; x < sizeX; x++)
            for (int y = 0; y < sizeY; y++)
                for (int z = 0; z < sizeZ; z++)
                    voxels[x, y, z] = new Voxel(0f);
    }

    public void RandomDistances()
    {
        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (x == 0 && y == 0 && z == 0)
                    {
                        voxels[x, y, z].distance = 5f;
                    }
                    else if (x == 1 && y == 0 && z == 0)
                    {
                        voxels[x, y, z].distance = 5f;
                    }
                    else
                    {
                        voxels[x, y, z].distance = 0f;
                    }
                }
            }
        }
    }
}


