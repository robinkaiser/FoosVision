// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Opengl;
using FoosVision.Common.Logging;
using Java.Nio;

namespace FoosVision.Media.Android.Common;

/// <summary>
/// Renders an external OES texture into the current EGL surface,
/// allowing GLES20.GlReadPixels to produce an RGBA buffer.
/// Based on bigflake's MediaCodec examples (https://bigflake.com/mediacodec).
/// </summary>
internal class TextureRender
{
    private const int _FloatSizeBytes = 4;
    private const int _StrideBytes = 5 * _FloatSizeBytes;
    private const int _PosOffset = 0;
    private const int _UvOffset = 3;

    private const string _VertexShader =
      "uniform mat4 uMVPMatrix;\n" +
      "uniform mat4 uSTMatrix;\n" +
      "attribute vec4 aPosition;\n" +
      "attribute vec4 aTextureCoord;\n" +
      "varying vec2 vTextureCoord;\n" +
      "void main() {\n" +
      "  gl_Position = uMVPMatrix * aPosition;\n" +
      "  vTextureCoord = (uSTMatrix * aTextureCoord).xy;\n" +
      "}\n";

    private const string _FragmentShader =
        "#extension GL_OES_EGL_image_external : require\n" +
        "precision mediump float;\n" +
        "varying vec2 vTextureCoord;\n" +
        "uniform samplerExternalOES sTexture;\n" +
        "void main() {\n" +
        "  gl_FragColor = texture2D(sTexture, vTextureCoord);\n" +
        "}\n";

    private static readonly Source _Log = new("TextureRender");

    private static readonly float[] _TriangleVerticesData =
    {
        // X,   Y,   Z,   U,   V
        -1.0f, -1.0f, 0, 0.0f, 0.0f,
         1.0f, -1.0f, 0, 1.0f, 0.0f,
        -1.0f,  1.0f, 0, 0.0f, 1.0f,
         1.0f,  1.0f, 0, 1.0f, 1.0f,
    };

    private readonly FloatBuffer _TriangleVertices;

    private readonly float[] _MvpMatrix = new float[16];
    private readonly float[] _StMatrix = new float[16];

    private int _Program;
    private int _TextureId = -1;

    private int _MvpMatrixHandle;
    private int _StMatrixHandle;
    private int _PositionHandle;
    private int _TexCoordHandle;

    public TextureRender()
    {
        _TriangleVertices = ByteBuffer.AllocateDirect(_TriangleVerticesData.Length * _FloatSizeBytes)
            .Order(ByteOrder.NativeOrder()!)
            .AsFloatBuffer();

        _ = _TriangleVertices.Put(_TriangleVerticesData)!.Position(0);

        Matrix.SetIdentityM(_StMatrix, 0);
    }

    public int TextureId => _TextureId;

    public void UpdateTransform(float[] stMatrix)
    {
        Array.Copy(stMatrix, 0, _StMatrix, 0, _StMatrix.Length);
    }

    public void SurfaceCreated()
    {
        _Program = CreateProgram(_VertexShader, _FragmentShader);

        _PositionHandle = GLES20.GlGetAttribLocation(_Program, "aPosition");
        _TexCoordHandle = GLES20.GlGetAttribLocation(_Program, "aTextureCoord");
        _MvpMatrixHandle = GLES20.GlGetUniformLocation(_Program, "uMVPMatrix");
        _StMatrixHandle = GLES20.GlGetUniformLocation(_Program, "uSTMatrix");

        CheckLocation(_PositionHandle, "aPosition");
        CheckLocation(_TexCoordHandle, "aTextureCoord");
        CheckLocation(_MvpMatrixHandle, "uMVPMatrix");
        CheckLocation(_StMatrixHandle, "uSTMatrix");

        var textures = new int[1];
        GLES20.GlGenTextures(1, textures, 0);
        _TextureId = textures[0];

        GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, _TextureId);

        GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMinFilter, GLES20.GlNearest);
        GLES20.GlTexParameterf(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureMagFilter, GLES20.GlLinear);
        GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapS, GLES20.GlClampToEdge);
        GLES20.GlTexParameteri(GLES11Ext.GlTextureExternalOes, GLES20.GlTextureWrapT, GLES20.GlClampToEdge);

        CheckGlError("SurfaceCreated");
    }

    public void DrawFrame()
    {
        // Optional: clear to green so we can see if we're failing to set pixels
        GLES20.GlClearColor(0, 1, 0, 1);
        GLES20.GlClear(GLES20.GlColorBufferBit);

        GLES20.GlUseProgram(_Program);
        if (CheckGlError("GlUseProgram")) return;

        GLES20.GlActiveTexture(GLES20.GlTexture0);
        if (CheckGlError("GlActiveTexture")) return;

        GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, _TextureId);
        if (CheckGlError("GlBindTexture")) return;

        _TriangleVertices.Position(_PosOffset);
        GLES20.GlVertexAttribPointer(_PositionHandle, 3, GLES20.GlFloat, false, _StrideBytes, _TriangleVertices);
        GLES20.GlEnableVertexAttribArray(_PositionHandle);

        _TriangleVertices.Position(_UvOffset);
        GLES20.GlVertexAttribPointer(_TexCoordHandle, 2, GLES20.GlFloat, false, _StrideBytes, _TriangleVertices);
        GLES20.GlEnableVertexAttribArray(_TexCoordHandle);

        Matrix.SetIdentityM(_MvpMatrix, 0);
        GLES20.GlUniformMatrix4fv(_MvpMatrixHandle, 1, false, _MvpMatrix, 0);
        GLES20.GlUniformMatrix4fv(_StMatrixHandle, 1, false, _StMatrix, 0);

        GLES20.GlDrawArrays(GLES20.GlTriangleStrip, 0, 4);

        GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, 0);
        CheckGlError("DrawFrame");
    }

    private static int CreateProgram(string vertexSource, string fragmentSource)
    {
        int vs = LoadShader(GLES20.GlVertexShader, vertexSource);
        int fs = LoadShader(GLES20.GlFragmentShader, fragmentSource);

        int program = GLES20.GlCreateProgram();

        if (program == 0)
        {
            _Log.Error("glCreateProgram returned 0");
            return 0;
        }

        GLES20.GlAttachShader(program, vs);
        GLES20.GlAttachShader(program, fs);
        GLES20.GlLinkProgram(program);

        int[] linkStatus = new int[1];
        GLES20.GlGetProgramiv(program, GLES20.GlLinkStatus, linkStatus, 0);
        if (linkStatus[0] != GLES20.GlTrue)
        {
            _Log.Error("Could not link program: {0}", GLES20.GlGetProgramInfoLog(program));
            GLES20.GlDeleteProgram(program);
            return 0;
        }

        return program;
    }

    private static int LoadShader(int shaderType, string source)
    {
        int shader = GLES20.GlCreateShader(shaderType);
        GLES20.GlShaderSource(shader, source);
        GLES20.GlCompileShader(shader);

        int[] compiled = new int[1];
        GLES20.GlGetShaderiv(shader, GLES20.GlCompileStatus, compiled, 0);

        if (compiled[0] == 0)
        {
            _Log.Error("Could not compile shader {0}: {1}", shaderType, GLES20.GlGetShaderInfoLog(shader));
            GLES20.GlDeleteShader(shader);
            return 0;
        }

        return shader;
    }

    private static void CheckLocation(int location, string label)
    {
        if (location < 0)
        {
            _Log.Error($"Unable to locate '{label}' in program");
            throw new InvalidOperationException($"Unable to locate '{label}' in shader.");
        }
    }

    private static bool CheckGlError(string op)
    {
        int error;

        while ((error = GLES20.GlGetError()) != GLES20.GlNoError)
        {
            _Log.Error("GL error after {0}: 0x{1}", op, error.ToString("X"));
            return true;
        }

        return false;
    }
}
