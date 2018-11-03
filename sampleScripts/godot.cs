//css_ref syncproj.exe
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Serialization;
//css_ref System.Web.Extensions.dll

partial class Builder : SolutionProjectBuilder
{
    static void SetCommonProjectSettings(String mainPlatform)
    {
        characterset("MBCS");
        objdir("obj/$(ProjectName)_$(Configuration)_$(Platform)");

        filter("Debug");
        defines(
            "DEBUG_MEMORY_ENABLED",
            "D3D_DEBUG_INFO",
            "DEBUG_MEMORY_ALLOC",
            "ZLIB_DEBUG"
        );
        filter();

        filter("(Release|ReleaseDebug)");
            CCpp_CodeGeneration_RuntimeLibrary(ERuntimeLibrary.MultiThreaded);

            if (mainPlatform == "windows")
                defines("NDEBUG");
        filter();

        defines(
            // Not possible to compile without this define: 
            // gd_editor.cpp(2558): error C2065: 'GDCompletionContext': undeclared identifier
            "DEBUG_ENABLED",
            "FT2_BUILD_LIBRARY",
            "FREETYPE_ENABLED",
            "SVG_ENABLED",
            "PTRCALL_ENABLED",
            "GDSCRIPT_ENABLED",
            "MINIZIP_ENABLED",
            "XML_ENABLED"
        );

        if (mainPlatform == "windows")
        {
            defines("WINDOWS_ENABLED;OPENGL_ENABLED;RTAUDIO_ENABLED;WASAPI_ENABLED;TYPED_METHOD_BIND");
            defines("WINVER=0x0601");
            defines("RECAST_ENABLED;SCI_NAMESPACE;MSVC;TOOLS_ENABLED;GLAD_ENABLED;GLES_OVER_GL");
        }
        else
        {
            toolset("Clang_3_8");
            defines("NO_SAFE_CAST;NO_STATVFS;GLES2_ENABLED;__ARM_ARCH_7__;__ARM_ARCH_7A__;__ARM_NEON__");
            defines("ANDROID_ENABLED;UNIX_ENABLED;NO_FCNTL;MPC_FIXED_POINT");
            // Disables all warnings. This is maybe not so good, but enabled by default in godot.
            buildoptions("-w");
        }
    }


    static void filesByFolder(String inFolder, params String[] fileList)
    {
        files(fileList.Select(x => Path.Combine(inFolder, x)).ToArray());
    }

    static void includeByFolder(String inFolder, params String[] dirList)
    {
        includedirs(dirList.Select(x => Path.Combine(inFolder, x)).ToArray());
    }

    /// <summary>
    /// Updates generated file on disk and add it to project.
    /// </summary>
    /// <param name="file">relative path to file</param>
    /// <param name="fileContent">file contents to save</param>
    static void addGeneratedFile( String file, String fileContent)
    {
        String filePath = Path.Combine(getCsDir(),file.Replace("/", "\\"));

        if (!File.Exists(filePath) || File.ReadAllText(file) != fileContent)
            File.WriteAllText(filePath, fileContent);

        files(file);
    }


    static void Main(String[] args)
    {
        addProject("windows");
        addProject("android");
    }

    static void platform2(String mainPlatform)
    { 
        vsver(2015);

        if( mainPlatform == "windows")
            platforms("Win32", "x64");
        else
            platforms("ARM64");
    }


    static void addProject( String mainPlatform )
    {
        solution("godot_" + mainPlatform);
        configurations("Debug", "Release", "ReleaseDebug");
        platform2(mainPlatform);
        project("godot_" + mainPlatform);

        platform2(mainPlatform);
        configurations("Debug", "Release", "ReleaseDebug");
        if( mainPlatform == "windows")
            kind("ConsoleApp", mainPlatform);
        else
            kind("DynamicLibrary", mainPlatform);

        projectScript("godot.cs");

        SetCommonProjectSettings(mainPlatform);
        targetdir("bin/$(Configuration)_$(Platform)");

        includedirs
        (
            "core",
            @"core\math",
            "editor",
            "drivers",
            ".",
            @"platform\" + mainPlatform,
            @"thirdparty\zstd",
            @"thirdparty\zstd\common",
            @"thirdparty\zlib",
            @"thirdparty\rtaudio",
            @"thirdparty\glad",
            @"thirdparty\freetype",
            @"thirdparty\freetype\include",
            @"thirdparty\libpng",
            @"thirdparty\recastnavigation\Recast",
            @"thirdparty\recastnavigation\Recast\Include",
            @"thirdparty\nanosvg",
            @"modules\gdnative\include"
        );

        // warning C4530: C++ exception handler used, but unwind semantics are not enabled. Specify /EHsc
        // warning C4577: 'noexcept' used with no exception handling mode specified; termination on exception is not guaranteed. Specify /EHsc
        disablewarnings("4291;4577;4530");

        String dir = getCsDir();

        List<String> exceptPaths = new string[]
        {
            @"drivers\xaudio2\",
            @"modules\mono\",
            @"modules\theora\",
            @"modules\webm\",
            @"modules\opus\",
            @"modules\ogg\",
            //@"modules\etc\",
            @"modules\vorbis\",
            @"thirdparty\libsimplewebm\",
            @"thirdparty\libogg\",
            @"thirdparty\libtheora\",
            @"thirdparty\libvorbis\",
            @"thirdparty\opus\",
            @"thirdparty\libvpx\",
            //@"modules\gdscript\gd_script.cpp",

            // Handled separately
            @"thirdparty\freetype\",
            @"thirdparty\openssl\",
            @"thirdparty\misc\",
            @"thirdparty\pcre2\"
        }.ToList();

        if (mainPlatform == "android")
        {
            exceptPaths.Add(@"editor\");
            exceptPaths.Add(@"modules\etc\");
            exceptPaths.Add(@"thirdparty\etc2comp\");
            exceptPaths.Add(@"modules\gridmap\");
        }

        foreach (String f in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
        {
            String ext = Path.GetExtension(f).ToLower();

            if (ext != ".h" && ext != ".cpp" && ext != ".c" && ext != ".cc")
                continue;

            // Inplace generated files, handled separately.
            if (f.EndsWith(".gen.cpp"))
                continue;
                
            String f2 = Path2.makeRelative(f, dir);

            // temporary files
            if (f2.StartsWith("."))
                continue;

            // Just some backup file.
            if (f2.Contains("Copy."))
                continue;

            if (f2.StartsWith("platform\\"))
            { 
                if(!f2.StartsWith("platform\\" + mainPlatform + "\\"))
                    continue;

                if (Path.GetFileNameWithoutExtension(f2) == "power_android")
                    continue;

                if( f2 == @"platform\android\export\export.cpp")
                    continue;
            }

            bool bInclude = true;

            foreach(String exceptDir in exceptPaths)
                if (f2.StartsWith(exceptDir))
                {
                    bInclude = false;
                    break;
                }

            if (!bInclude)
                continue;

            files(f2);

            if (f2.StartsWith("modules\\") && ext == ".cpp")
            {
                String[] fsparts = f2.Split('\\');
                String dir3party = fsparts[1];
                String extraDefines = null;

                if (fsparts.Length >= 3 )
                {
                    switch (dir3party)
                    {
                        case "enet": extraDefines = "GODOT_ENET"; break;
                        case "webp": dir3party = "libwebp"; break;
                        case "pvr": dir3party = "pvrtccompressor"; break;
                        case "etc": dir3party = "etc2comp"; break;
                        case "jpg": dir3party = "jpeg-compressor"; break;
                        case "regex":
                            extraDefines = "PCRE2_CODE_UNIT_WIDTH=0;PCRE2_STATIC";
                            dir3party = @"pcre2\src"; 
                            break;
                        case "gdnative":
                            break;
                    }

                    String incDir = Path.Combine("thirdparty", dir3party);
                    if (Directory.Exists(incDir))
                    {
                        filter("files:" + f2);    
                        includedirs(incDir);
                        filter();
                    }

                    if (extraDefines != null)
                    { 
                        filter("files:" + f2);
                        defines(extraDefines);
                        filter();
                    }
                }
            } //if
        } //foreach file.

        filter(@"files:thirdparty\enet\*.c*");
        includedirs(@"thirdparty\enet");
        filter();

        String csDir = getCsDir();
        String projDir = Path.Combine(csDir, "thirdparty\\pcre2\\src");
        
        String[] pcreFiles = new String[]
        {
            "pcre2_auto_possess.c",
            "pcre2_chartables.c",
            "pcre2_compile.c",
            "pcre2_config.c",
            "pcre2_context.c",
            "pcre2_dfa_match.c",
            "pcre2_error.c",
            "pcre2_find_bracket.c",
            "pcre2_jit_compile.c",
            "pcre2_maketables.c",
            "pcre2_match.c",
            "pcre2_match_data.c",
            "pcre2_newline.c",
            "pcre2_ord2utf.c",
            "pcre2_pattern_info.c",
            "pcre2_serialize.c",
            "pcre2_string_utils.c",
            "pcre2_study.c",
            "pcre2_substitute.c",
            "pcre2_substring.c",
            "pcre2_tables.c",
            "pcre2_ucd.c",
            "pcre2_valid_utf.c",
            "pcre2_xclass.c"
        };

        foreach (String pcrevar in new String[] { "16", "32" })
        {
            var oldP = m_project;
            m_project = null;

            String pcreProjName = "pcre2_" + pcrevar + "_" + mainPlatform;

            project(pcreProjName);
            location(projDir);
            platform2(mainPlatform);
            kind("StaticLibrary", mainPlatform);
            vsver(2015);
            files(pcreFiles);
            SetCommonProjectSettings(mainPlatform);
            language("C");
            defines("PCRE2_STATIC", "HAVE_CONFIG_H", "SUPPORT_JIT", "PCRE2_CODE_UNIT_WIDTH=" + pcrevar);

            project(null);
            m_project = oldP;

            references("thirdparty\\pcre2\\src\\" + pcreProjName + ".vcxproj", "");
        }

        filesByFolder( @"thirdparty\freetype",
            "src/autofit/autofit.c",
            "src/base/ftapi.c",
            "src/base/ftbase.c",
            "src/base/ftbbox.c",
            "src/base/ftbdf.c",
            "src/base/ftbitmap.c",
            "src/base/ftcid.c",
            "src/base/ftdebug.c",
            "src/base/ftfntfmt.c",
            "src/base/ftfstype.c",
            "src/base/ftgasp.c",
            "src/base/ftglyph.c",
            "src/base/ftgxval.c",
            "src/base/ftinit.c",
            "src/base/ftlcdfil.c",
            "src/base/ftmm.c",
            "src/base/ftotval.c",
            "src/base/ftpatent.c",
            "src/base/ftpfr.c",
            "src/base/ftpic.c",
            "src/base/ftstroke.c",
            "src/base/ftsynth.c",
            "src/base/ftsystem.c",
            "src/base/fttype1.c",
            "src/base/ftwinfnt.c",
            "src/bdf/bdf.c",
            "src/bzip2/ftbzip2.c",
            "src/cache/ftcache.c",
            "src/cff/cff.c",
            "src/cid/type1cid.c",
            "src/gxvalid/gxvalid.c",
            "src/gzip/ftgzip.c",
            "src/lzw/ftlzw.c",
            "src/otvalid/otvalid.c",
            "src/pcf/pcf.c",
            "src/pfr/pfr.c",
            "src/psaux/psaux.c",
            "src/pshinter/pshinter.c",
            "src/psnames/psnames.c",
            "src/raster/raster.c",
            "src/sfnt/sfnt.c",
            "src/smooth/smooth.c",
            "src/truetype/truetype.c",
            "src/type1/type1.c",
            "src/type42/type42.c",
            "src/winfonts/winfnt.c"
         );

        filesByFolder(@"thirdparty\openssl",
            "ssl/t1_lib.c",
            "ssl/t1_ext.c",
            "ssl/s3_srvr.c",
            "ssl/t1_enc.c",
            "ssl/t1_meth.c",
            "ssl/s23_clnt.c",
            "ssl/ssl_asn1.c",
            "ssl/tls_srp.c",
            "ssl/kssl.c",
            "ssl/d1_both.c",
            "ssl/t1_clnt.c",
            "ssl/bio_ssl.c",
            "ssl/d1_srtp.c",
            "ssl/t1_reneg.c",
            "ssl/ssl_cert.c",
            "ssl/s3_lib.c",
            "ssl/d1_srvr.c",
            "ssl/s23_meth.c",
            "ssl/ssl_stat.c",
            "ssl/ssl_err.c",
            "ssl/ssl_algs.c",
            "ssl/s3_cbc.c",
            "ssl/d1_clnt.c",
            "ssl/s3_pkt.c",
            "ssl/d1_meth.c",
            "ssl/s3_both.c",
            "ssl/s2_enc.c",
            "ssl/s3_meth.c",
            "ssl/s3_enc.c",
            "ssl/s23_pkt.c",
            "ssl/s2_pkt.c",
            "ssl/d1_pkt.c",
            "ssl/ssl_rsa.c",
            "ssl/s23_srvr.c",
            "ssl/s2_meth.c",
            "ssl/s3_clnt.c",
            "ssl/s23_lib.c",
            "ssl/t1_srvr.c",
            "ssl/ssl_lib.c",
            "ssl/ssl_txt.c",
            "ssl/s2_srvr.c",
            "ssl/ssl_sess.c",
            "ssl/s2_clnt.c",
            "ssl/d1_lib.c",
            "ssl/s2_lib.c",
            "ssl/ssl_err2.c",
            "ssl/ssl_ciph.c",
            "crypto/dsa/dsa_lib.c",
            "crypto/dsa/dsa_pmeth.c",
            "crypto/dsa/dsa_ossl.c",
            "crypto/dsa/dsa_gen.c",
            "crypto/dsa/dsa_asn1.c",
            "crypto/dsa/dsa_prn.c",
            "crypto/dsa/dsa_sign.c",
            "crypto/dsa/dsa_key.c",
            "crypto/dsa/dsa_vrf.c",
            "crypto/dsa/dsa_err.c",
            "crypto/dsa/dsa_ameth.c",
            "crypto/dsa/dsa_depr.c",
            "crypto/x509/x509_lu.c",
            "crypto/x509/x509cset.c",
            "crypto/x509/x509_set.c",
            "crypto/x509/x509_d2.c",
            "crypto/x509/x509_txt.c",
            "crypto/x509/x509rset.c",
            "crypto/x509/by_dir.c",
            "crypto/x509/x509_vpm.c",
            "crypto/x509/x509_vfy.c",
            "crypto/x509/x509_trs.c",
            "crypto/x509/by_file.c",
            "crypto/x509/x509_obj.c",
            "crypto/x509/x509spki.c",
            "crypto/x509/x509_v3.c",
            "crypto/x509/x509_req.c",
            "crypto/x509/x509_att.c",
            "crypto/x509/x_all.c",
            "crypto/x509/x509_ext.c",
            "crypto/x509/x509type.c",
            "crypto/x509/x509_def.c",
            "crypto/x509/x509_err.c",
            "crypto/x509/x509name.c",
            "crypto/x509/x509_r2x.c",
            "crypto/x509/x509_cmp.c",
            "crypto/asn1/x_pkey.c",
            "crypto/asn1/a_gentm.c",
            "crypto/asn1/x_sig.c",
            "crypto/asn1/t_req.c",
            "crypto/asn1/t_pkey.c",
            "crypto/asn1/p8_pkey.c",
            "crypto/asn1/a_i2d_fp.c",
            "crypto/asn1/x_val.c",
            "crypto/asn1/f_string.c",
            "crypto/asn1/p5_pbe.c",
            "crypto/asn1/bio_ndef.c",
            "crypto/asn1/a_bool.c",
            "crypto/asn1/asn1_gen.c",
            "crypto/asn1/x_algor.c",
            "crypto/asn1/bio_asn1.c",
            "crypto/asn1/asn_mime.c",
            "crypto/asn1/t_x509.c",
            "crypto/asn1/a_strex.c",
            "crypto/asn1/x_nx509.c",
            "crypto/asn1/asn1_err.c",
            "crypto/asn1/x_crl.c",
            "crypto/asn1/a_print.c",
            "crypto/asn1/a_type.c",
            "crypto/asn1/tasn_new.c",
            "crypto/asn1/n_pkey.c",
            "crypto/asn1/x_bignum.c",
            "crypto/asn1/asn_pack.c",
            "crypto/asn1/evp_asn1.c",
            "crypto/asn1/t_bitst.c",
            "crypto/asn1/x_req.c",
            "crypto/asn1/a_time.c",
            "crypto/asn1/x_name.c",
            "crypto/asn1/x_pubkey.c",
            "crypto/asn1/tasn_typ.c",
            "crypto/asn1/asn_moid.c",
            "crypto/asn1/a_utctm.c",
            "crypto/asn1/asn1_lib.c",
            "crypto/asn1/x_x509a.c",
            "crypto/asn1/a_set.c",
            "crypto/asn1/t_crl.c",
            "crypto/asn1/p5_pbev2.c",
            "crypto/asn1/tasn_enc.c",
            "crypto/asn1/a_mbstr.c",
            "crypto/asn1/tasn_dec.c",
            "crypto/asn1/x_x509.c",
            "crypto/asn1/a_octet.c",
            "crypto/asn1/x_long.c",
            "crypto/asn1/a_bytes.c",
            "crypto/asn1/t_x509a.c",
            "crypto/asn1/a_enum.c",
            "crypto/asn1/a_int.c",
            "crypto/asn1/tasn_prn.c",
            "crypto/asn1/i2d_pr.c",
            "crypto/asn1/a_utf8.c",
            "crypto/asn1/t_spki.c",
            "crypto/asn1/a_digest.c",
            "crypto/asn1/a_dup.c",
            "crypto/asn1/i2d_pu.c",
            "crypto/asn1/a_verify.c",
            "crypto/asn1/f_enum.c",
            "crypto/asn1/a_sign.c",
            "crypto/asn1/d2i_pr.c",
            "crypto/asn1/asn1_par.c",
            "crypto/asn1/x_spki.c",
            "crypto/asn1/a_d2i_fp.c",
            "crypto/asn1/f_int.c",
            "crypto/asn1/x_exten.c",
            "crypto/asn1/tasn_utl.c",
            "crypto/asn1/nsseq.c",
            "crypto/asn1/a_bitstr.c",
            "crypto/asn1/x_info.c",
            "crypto/asn1/a_strnid.c",
            "crypto/asn1/a_object.c",
            "crypto/asn1/tasn_fre.c",
            "crypto/asn1/d2i_pu.c",
            "crypto/asn1/ameth_lib.c",
            "crypto/asn1/x_attrib.c",
            "crypto/evp/m_sha.c",
            "crypto/evp/e_camellia.c",
            "crypto/evp/e_aes.c",
            "crypto/evp/bio_b64.c",
            "crypto/evp/m_sigver.c",
            "crypto/evp/m_wp.c",
            "crypto/evp/m_sha1.c",
            "crypto/evp/p_seal.c",
            "crypto/evp/c_alld.c",
            "crypto/evp/p5_crpt.c",
            "crypto/evp/e_rc4.c",
            "crypto/evp/m_ecdsa.c",
            "crypto/evp/bio_enc.c",
            "crypto/evp/e_des3.c",
            "crypto/evp/m_null.c",
            "crypto/evp/bio_ok.c",
            "crypto/evp/pmeth_gn.c",
            "crypto/evp/e_rc5.c",
            "crypto/evp/e_rc2.c",
            "crypto/evp/p_dec.c",
            "crypto/evp/p_verify.c",
            "crypto/evp/e_rc4_hmac_md5.c",
            "crypto/evp/pmeth_lib.c",
            "crypto/evp/m_ripemd.c",
            "crypto/evp/m_md5.c",
            "crypto/evp/e_bf.c",
            "crypto/evp/p_enc.c",
            "crypto/evp/m_dss.c",
            "crypto/evp/bio_md.c",
            "crypto/evp/evp_pbe.c",
            "crypto/evp/e_seed.c",
            "crypto/evp/e_cast.c",
            "crypto/evp/p_open.c",
            "crypto/evp/p5_crpt2.c",
            "crypto/evp/m_dss1.c",
            "crypto/evp/names.c",
            "crypto/evp/evp_acnf.c",
            "crypto/evp/e_des.c",
            "crypto/evp/evp_cnf.c",
            "crypto/evp/evp_lib.c",
            "crypto/evp/digest.c",
            "crypto/evp/evp_err.c",
            "crypto/evp/evp_enc.c",
            "crypto/evp/e_old.c",
            "crypto/evp/c_all.c",
            "crypto/evp/m_md2.c",
            "crypto/evp/e_xcbc_d.c",
            "crypto/evp/pmeth_fn.c",
            "crypto/evp/p_lib.c",
            "crypto/evp/evp_key.c",
            "crypto/evp/encode.c",
            "crypto/evp/e_aes_cbc_hmac_sha1.c",
            "crypto/evp/e_aes_cbc_hmac_sha256.c",
            "crypto/evp/m_mdc2.c",
            "crypto/evp/e_null.c",
            "crypto/evp/p_sign.c",
            "crypto/evp/e_idea.c",
            "crypto/evp/c_allc.c",
            "crypto/evp/evp_pkey.c",
            "crypto/evp/m_md4.c",
            "crypto/ex_data.c",
            "crypto/pkcs12/p12_p8e.c",
            "crypto/pkcs12/p12_crt.c",
            "crypto/pkcs12/p12_utl.c",
            "crypto/pkcs12/p12_attr.c",
            "crypto/pkcs12/p12_npas.c",
            "crypto/pkcs12/p12_decr.c",
            "crypto/pkcs12/p12_init.c",
            "crypto/pkcs12/p12_kiss.c",
            "crypto/pkcs12/p12_add.c",
            "crypto/pkcs12/p12_p8d.c",
            "crypto/pkcs12/p12_mutl.c",
            "crypto/pkcs12/p12_crpt.c",
            "crypto/pkcs12/pk12err.c",
            "crypto/pkcs12/p12_asn.c",
            "crypto/pkcs12/p12_key.c",
            "crypto/ecdh/ech_key.c",
            "crypto/ecdh/ech_ossl.c",
            "crypto/ecdh/ech_lib.c",
            "crypto/ecdh/ech_err.c",
            "crypto/ecdh/ech_kdf.c",
            "crypto/o_str.c",
            "crypto/conf/conf_api.c",
            "crypto/conf/conf_err.c",
            "crypto/conf/conf_def.c",
            "crypto/conf/conf_lib.c",
            "crypto/conf/conf_mall.c",
            "crypto/conf/conf_sap.c",
            "crypto/conf/conf_mod.c",
            "crypto/ebcdic.c",
            "crypto/ecdsa/ecs_lib.c",
            "crypto/ecdsa/ecs_asn1.c",
            "crypto/ecdsa/ecs_ossl.c",
            "crypto/ecdsa/ecs_vrf.c",
            "crypto/ecdsa/ecs_sign.c",
            "crypto/ecdsa/ecs_err.c",
            "crypto/dso/dso_win32.c",
            "crypto/dso/dso_lib.c",
            "crypto/dso/dso_dlfcn.c",
            "crypto/dso/dso_dl.c",
            "crypto/dso/dso_beos.c",
            "crypto/dso/dso_null.c",
            "crypto/dso/dso_vms.c",
            "crypto/dso/dso_err.c",
            "crypto/dso/dso_openssl.c",
            "crypto/cryptlib.c",
            "crypto/md5/md5_one.c",
            "crypto/md5/md5_dgst.c",
            "crypto/pkcs7/pkcs7err.c",
            "crypto/pkcs7/pk7_smime.c",
            "crypto/pkcs7/bio_pk7.c",
            "crypto/pkcs7/pk7_mime.c",
            "crypto/pkcs7/pk7_lib.c",
            "crypto/pkcs7/pk7_asn1.c",
            "crypto/pkcs7/pk7_doit.c",
            "crypto/pkcs7/pk7_attr.c",
            "crypto/md4/md4_one.c",
            "crypto/md4/md4_dgst.c",
            "crypto/o_dir.c",
            "crypto/buffer/buf_err.c",
            "crypto/buffer/buf_str.c",
            "crypto/buffer/buffer.c",
            "crypto/cms/cms_lib.c",
            "crypto/cms/cms_io.c",
            "crypto/cms/cms_err.c",
            "crypto/cms/cms_dd.c",
            "crypto/cms/cms_smime.c",
            "crypto/cms/cms_att.c",
            "crypto/cms/cms_pwri.c",
            "crypto/cms/cms_cd.c",
            "crypto/cms/cms_sd.c",
            "crypto/cms/cms_asn1.c",
            "crypto/cms/cms_env.c",
            "crypto/cms/cms_enc.c",
            "crypto/cms/cms_ess.c",
            "crypto/cms/cms_kari.c",
            "crypto/mem_dbg.c",
            "crypto/uid.c",
            "crypto/stack/stack.c",
            "crypto/ec/ec_ameth.c",
            "crypto/ec/ec_err.c",
            "crypto/ec/ec_lib.c",
            "crypto/ec/ec_curve.c",
            "crypto/ec/ec_oct.c",
            "crypto/ec/ec_asn1.c",
            "crypto/ec/ecp_oct.c",
            "crypto/ec/ec_print.c",
            "crypto/ec/ec2_smpl.c",
            "crypto/ec/ecp_nistp224.c",
            "crypto/ec/ec2_oct.c",
            "crypto/ec/eck_prn.c",
            "crypto/ec/ec_key.c",
            "crypto/ec/ecp_nist.c",
            "crypto/ec/ec_check.c",
            "crypto/ec/ecp_smpl.c",
            "crypto/ec/ec2_mult.c",
            "crypto/ec/ecp_mont.c",
            "crypto/ec/ecp_nistp521.c",
            "crypto/ec/ec_mult.c",
            "crypto/ec/ecp_nistputil.c",
            "crypto/ec/ec_pmeth.c",
            "crypto/ec/ec_cvt.c",
            "crypto/ec/ecp_nistp256.c",
            "crypto/krb5/krb5_asn.c",
            "crypto/hmac/hmac.c",
            "crypto/hmac/hm_ameth.c",
            "crypto/hmac/hm_pmeth.c",
            "crypto/comp/c_rle.c",
            "crypto/comp/c_zlib.c",
            "crypto/comp/comp_lib.c",
            "crypto/comp/comp_err.c",
            "crypto/des/fcrypt.c",
            "crypto/des/str2key.c",
            "crypto/des/cbc_cksm.c",
            "crypto/des/des_enc.c",
            "crypto/des/ofb_enc.c",
            "crypto/des/read2pwd.c",
            "crypto/des/ecb3_enc.c",
            "crypto/des/rand_key.c",
            "crypto/des/cfb64ede.c",
            "crypto/des/rpc_enc.c",
            "crypto/des/ofb64ede.c",
            "crypto/des/qud_cksm.c",
            "crypto/des/enc_writ.c",
            "crypto/des/set_key.c",
            "crypto/des/xcbc_enc.c",
            "crypto/des/fcrypt_b.c",
            "crypto/des/ede_cbcm_enc.c",
            "crypto/des/des_old2.c",
            "crypto/des/cfb_enc.c",
            "crypto/des/ecb_enc.c",
            "crypto/des/enc_read.c",
            "crypto/des/des_old.c",
            "crypto/des/ofb64enc.c",
            "crypto/des/pcbc_enc.c",
            "crypto/des/cbc_enc.c",
            "crypto/des/cfb64enc.c",
            "crypto/lhash/lh_stats.c",
            "crypto/lhash/lhash.c",
            "crypto/x509v3/v3_genn.c",
            "crypto/x509v3/pcy_cache.c",
            "crypto/x509v3/v3_sxnet.c",
            "crypto/x509v3/v3_scts.c",
            "crypto/x509v3/v3err.c",
            "crypto/x509v3/v3_conf.c",
            "crypto/x509v3/v3_utl.c",
            "crypto/x509v3/v3_akeya.c",
            "crypto/x509v3/v3_lib.c",
            "crypto/x509v3/pcy_lib.c",
            "crypto/x509v3/v3_cpols.c",
            "crypto/x509v3/v3_ia5.c",
            "crypto/x509v3/v3_bitst.c",
            "crypto/x509v3/v3_skey.c",
            "crypto/x509v3/v3_info.c",
            "crypto/x509v3/v3_asid.c",
            "crypto/x509v3/pcy_tree.c",
            "crypto/x509v3/v3_pcons.c",
            "crypto/x509v3/v3_bcons.c",
            "crypto/x509v3/v3_pku.c",
            "crypto/x509v3/v3_ocsp.c",
            "crypto/x509v3/pcy_map.c",
            "crypto/x509v3/v3_ncons.c",
            "crypto/x509v3/v3_purp.c",
            "crypto/x509v3/v3_enum.c",
            "crypto/x509v3/v3_pmaps.c",
            "crypto/x509v3/pcy_node.c",
            "crypto/x509v3/v3_pcia.c",
            "crypto/x509v3/v3_crld.c",
            "crypto/x509v3/v3_pci.c",
            "crypto/x509v3/v3_akey.c",
            "crypto/x509v3/v3_addr.c",
            "crypto/x509v3/v3_int.c",
            "crypto/x509v3/v3_alt.c",
            "crypto/x509v3/v3_extku.c",
            "crypto/x509v3/v3_prn.c",
            "crypto/x509v3/pcy_data.c",
            "crypto/aes/aes_ofb.c",
            "crypto/aes/aes_ctr.c",
            "crypto/aes/aes_ecb.c",
            "crypto/aes/aes_cfb.c",
            "crypto/aes/aes_wrap.c",
            "crypto/aes/aes_ige.c",
            "crypto/aes/aes_misc.c",
            "crypto/pqueue/pqueue.c",
            "crypto/sha/sha_one.c",
            "crypto/sha/sha_dgst.c",
            "crypto/sha/sha512.c",
            "crypto/sha/sha1_one.c",
            "crypto/sha/sha1dgst.c",
            "crypto/sha/sha256.c",
            "crypto/whrlpool/wp_dgst.c",
            "crypto/objects/obj_xref.c",
            "crypto/objects/o_names.c",
            "crypto/objects/obj_err.c",
            "crypto/objects/obj_dat.c",
            "crypto/objects/obj_lib.c",
            "crypto/mem.c",
            "crypto/fips_ers.c",
            "crypto/o_fips.c",
            "crypto/engine/eng_rdrand.c",
            "crypto/engine/eng_err.c",
            "crypto/engine/tb_ecdsa.c",
            "crypto/engine/tb_rsa.c",
            "crypto/engine/tb_cipher.c",
            "crypto/engine/tb_dsa.c",
            "crypto/engine/eng_lib.c",
            "crypto/engine/tb_asnmth.c",
            "crypto/engine/tb_ecdh.c",
            "crypto/engine/tb_dh.c",
            "crypto/engine/tb_store.c",
            "crypto/engine/eng_init.c",
            "crypto/engine/eng_cnf.c",
            "crypto/engine/eng_all.c",
            "crypto/engine/tb_digest.c",
            "crypto/engine/tb_pkmeth.c",
            "crypto/engine/eng_table.c",
            "crypto/engine/eng_ctrl.c",
            "crypto/engine/eng_list.c",
            "crypto/engine/eng_cryptodev.c",
            "crypto/engine/eng_pkey.c",
            "crypto/engine/tb_rand.c",
            "crypto/engine/eng_openssl.c",
            "crypto/engine/eng_fat.c",
            "crypto/engine/eng_dyn.c",
            "crypto/ts/ts_rsp_verify.c",
            "crypto/ts/ts_req_print.c",
            "crypto/ts/ts_verify_ctx.c",
            "crypto/ts/ts_req_utils.c",
            "crypto/ts/ts_err.c",
            "crypto/ts/ts_rsp_print.c",
            "crypto/ts/ts_rsp_utils.c",
            "crypto/ts/ts_lib.c",
            "crypto/ts/ts_conf.c",
            "crypto/ts/ts_asn1.c",
            "crypto/ts/ts_rsp_sign.c",
            "crypto/ocsp/ocsp_ext.c",
            "crypto/ocsp/ocsp_cl.c",
            "crypto/ocsp/ocsp_ht.c",
            "crypto/ocsp/ocsp_lib.c",
            "crypto/ocsp/ocsp_srv.c",
            "crypto/ocsp/ocsp_vfy.c",
            "crypto/ocsp/ocsp_err.c",
            "crypto/ocsp/ocsp_prn.c",
            "crypto/ocsp/ocsp_asn.c",
            "crypto/bf/bf_cfb64.c",
            "crypto/bf/bf_ecb.c",
            "crypto/bf/bf_enc.c",
            "crypto/bf/bf_skey.c",
            "crypto/bf/bf_ofb64.c",
            "crypto/idea/i_skey.c",
            "crypto/idea/i_ofb64.c",
            "crypto/idea/i_cbc.c",
            "crypto/idea/i_ecb.c",
            "crypto/idea/i_cfb64.c",
            "crypto/cmac/cm_ameth.c",
            "crypto/cmac/cmac.c",
            "crypto/cmac/cm_pmeth.c",
            "crypto/dh/dh_lib.c",
            "crypto/dh/dh_key.c",
            "crypto/dh/dh_asn1.c",
            "crypto/dh/dh_depr.c",
            "crypto/dh/dh_pmeth.c",
            "crypto/dh/dh_prn.c",
            "crypto/dh/dh_gen.c",
            "crypto/dh/dh_ameth.c",
            "crypto/dh/dh_check.c",
            "crypto/dh/dh_err.c",
            "crypto/dh/dh_kdf.c",
            "crypto/dh/dh_rfc5114.c",
            "crypto/modes/ccm128.c",
            "crypto/modes/ofb128.c",
            "crypto/modes/cts128.c",
            "crypto/modes/ctr128.c",
            "crypto/modes/gcm128.c",
            "crypto/modes/cbc128.c",
            "crypto/modes/cfb128.c",
            "crypto/modes/xts128.c",
            "crypto/modes/wrap128.c",
            "crypto/camellia/cmll_cfb.c",
            "crypto/camellia/cmll_ecb.c",
            "crypto/camellia/cmll_utl.c",
            "crypto/camellia/cmll_misc.c",
            "crypto/camellia/cmll_ofb.c",
            "crypto/camellia/cmll_ctr.c",
            "crypto/seed/seed_ecb.c",
            "crypto/seed/seed_cbc.c",
            "crypto/seed/seed.c",
            "crypto/seed/seed_ofb.c",
            "crypto/seed/seed_cfb.c",
            "crypto/txt_db/txt_db.c",
            "crypto/cpt_err.c",
            "crypto/pem/pem_pk8.c",
            "crypto/pem/pem_lib.c",
            "crypto/pem/pem_sign.c",
            "crypto/pem/pem_all.c",
            "crypto/pem/pem_info.c",
            "crypto/pem/pem_pkey.c",
            "crypto/pem/pem_seal.c",
            "crypto/pem/pem_err.c",
            "crypto/pem/pem_xaux.c",
            "crypto/pem/pvkfmt.c",
            "crypto/pem/pem_x509.c",
            "crypto/pem/pem_oth.c",
            "crypto/rand/rand_lib.c",
            "crypto/rand/randfile.c",
            "crypto/rand/rand_os2.c",
            "crypto/rand/rand_unix.c",
            "crypto/rand/rand_nw.c",
            "crypto/rand/md_rand.c",
            "crypto/rand/rand_err.c",
            "crypto/rand/rand_win.c",
            "crypto/rand/rand_egd.c",
            "crypto/cversion.c",
            "crypto/cast/c_ecb.c",
            "crypto/cast/c_skey.c",
            "crypto/cast/c_ofb64.c",
            "crypto/cast/c_enc.c",
            "crypto/cast/c_cfb64.c",
            "crypto/o_time.c",
            "crypto/mdc2/mdc2dgst.c",
            "crypto/mdc2/mdc2_one.c",
            "crypto/rc4/rc4_utl.c",
            "crypto/ui/ui_compat.c",
            "crypto/ui/ui_util.c",
            "crypto/ui/ui_lib.c",
            "crypto/ui/ui_err.c",
            "crypto/ui/ui_openssl.c",
            "crypto/bio/bf_buff.c",
            "crypto/bio/bss_null.c",
            "crypto/bio/bss_acpt.c",
            "crypto/bio/bss_conn.c",
            "crypto/bio/bss_fd.c",
            "crypto/bio/bf_null.c",
            "crypto/bio/bio_err.c",
            "crypto/bio/bss_sock.c",
            "crypto/bio/bss_mem.c",
            "crypto/bio/b_dump.c",
            "crypto/bio/b_print.c",
            "crypto/bio/b_sock.c",
            "crypto/bio/bss_dgram.c",
            "crypto/bio/bf_nbio.c",
            "crypto/bio/bio_lib.c",
            "crypto/bio/bss_file.c",
            "crypto/bio/bss_bio.c",
            "crypto/bio/bss_log.c",
            "crypto/bio/bio_cb.c",
            "crypto/o_init.c",
            "crypto/rc2/rc2_skey.c",
            "crypto/rc2/rc2_cbc.c",
            "crypto/rc2/rc2cfb64.c",
            "crypto/rc2/rc2_ecb.c",
            "crypto/rc2/rc2ofb64.c",
            "crypto/bn/bn_x931p.c",
            "crypto/bn/bn_blind.c",
            "crypto/bn/bn_gf2m.c",
            "crypto/bn/bn_const.c",
            "crypto/bn/bn_sqr.c",
            "crypto/bn/bn_nist.c",
            "crypto/bn/bn_rand.c",
            "crypto/bn/bn_err.c",
            "crypto/bn/bn_div.c",
            "crypto/bn/bn_kron.c",
            "crypto/bn/bn_ctx.c",
            "crypto/bn/bn_shift.c",
            "crypto/bn/bn_mod.c",
            "crypto/bn/bn_exp2.c",
            "crypto/bn/bn_word.c",
            "crypto/bn/bn_add.c",
            "crypto/bn/bn_exp.c",
            "crypto/bn/bn_mont.c",
            "crypto/bn/bn_print.c",
            "crypto/bn/bn_mul.c",
            "crypto/bn/bn_prime.c",
            "crypto/bn/bn_depr.c",
            "crypto/bn/bn_gcd.c",
            "crypto/bn/bn_mpi.c",
            "crypto/bn/bn_sqrt.c",
            "crypto/bn/bn_recp.c",
            "crypto/bn/bn_lib.c",
            "crypto/ripemd/rmd_dgst.c",
            "crypto/ripemd/rmd_one.c",
            "crypto/rsa/rsa_x931.c",
            "crypto/rsa/rsa_depr.c",
            "crypto/rsa/rsa_saos.c",
            "crypto/rsa/rsa_crpt.c",
            "crypto/rsa/rsa_pss.c",
            "crypto/rsa/rsa_oaep.c",
            "crypto/rsa/rsa_null.c",
            "crypto/rsa/rsa_gen.c",
            "crypto/rsa/rsa_prn.c",
            "crypto/rsa/rsa_pmeth.c",
            "crypto/rsa/rsa_asn1.c",
            "crypto/rsa/rsa_ssl.c",
            "crypto/rsa/rsa_ameth.c",
            "crypto/rsa/rsa_pk1.c",
            "crypto/rsa/rsa_err.c",
            "crypto/rsa/rsa_lib.c",
            "crypto/rsa/rsa_none.c",
            "crypto/rsa/rsa_chk.c",
            "crypto/rsa/rsa_eay.c",
            "crypto/rsa/rsa_sign.c",
            "crypto/srp/srp_lib.c",
            "crypto/srp/srp_vfy.c",
            "crypto/err/err.c",
            "crypto/err/err_prn.c",
            "crypto/err/err_all.c",
            "crypto/mem_clr.c",
            "crypto/rc4/rc4_skey.c",
            "crypto/rc4/rc4_enc.c",
            "crypto/camellia/camellia.c",
            "crypto/camellia/cmll_cbc.c",
            "crypto/aes/aes_core.c",
            "crypto/aes/aes_cbc.c",
            "crypto/whrlpool/wp_block.c",
            "crypto/bn/bn_asm.c"
        );

        filter("files:thirdparty\\openssl\\**");
        includeByFolder(@"thirdparty\\openssl",
            "",
            "crypto",
            "crypto/asn1",
            "crypto/evp",
            "crypto/modes",
            "openssl"
        );
        filter();

        filter(@"files:thirdparty\openssl\crypto\engine\eng_all.c");
        defines("OPENSSL_NO_HW;OPENSSL_NO_CAPIENG;OPENSSL_NO_GOST");
        filter();

        filesByFolder(@"thirdparty\\openssl", "**.h");

        filesByFolder("thirdparty/misc",
            "base64.c",
            "fastlz.c",
            "sha256.c",
            "smaz.c",
            "aes256.cpp",
            "hq2x.cpp",
            "md5.cpp",
            "pcg.cpp",
            "triangulator.cpp",

            // modules/openssl references:
            "curl_hostcheck.c",
        
            "mikktspace.c"
        );

        // ----------------------------------------------------------------------------
        //  Generate platform list.
        // ----------------------------------------------------------------------------
        StringBuilder sb = new StringBuilder();
        String lf = "\r\n";

        if (mainPlatform == "windows")
        {
            sb.Append("#include \"register_exporters.h\"" + lf);

            String[] platList = new String[] { "android", "iphone", "javascript", "osx", "uwp", "windows", "x11" };

            foreach (String platform in platList)
                sb.Append("#include \"platform/" + platform + "/export/export.h\"" + lf);

            sb.Append("void register_exporters() {" + lf);
            foreach (String platform in platList)
                sb.Append("\tregister_" + platform + "_exporter();" + lf);
            sb.Append("}" + lf);

            addGeneratedFile("editor/register_exporters.gen.cpp", sb.ToString());

            foreach (String platform in platList)
                files("platform/" + platform + "/export/export.cpp");

            files( @"platform\android\globals\global_defaults.cpp" );
        }

        files( @"platform\iphone\globals\global_defaults.cpp" );

        // ----------------------------------------------------------------------------
        //  Generate module list.
        // ----------------------------------------------------------------------------
        sb.Clear();
        String modDir = csDir + "\\modules";
        String[] mods = Directory.GetDirectories(modDir, "*").Select(x => Path2.makeRelative(x, modDir)).ToArray();

        sb.AppendLine();
        sb.AppendLine("// modules.cpp - THIS FILE IS GENERATED, DO NOT EDIT!!!!!!!");
        sb.AppendLine("#include \"register_module_types.h\"");
        sb.AppendLine();
        sb.AppendLine();
        foreach (String mod in mods)
            sb.AppendLine("#include \"modules/" + mod + "/register_types.h\"");

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("void register_module_types() {" + lf);
        foreach (String mod in mods)
        {
            sb.AppendLine("#ifdef MODULE_" + mod.ToUpper() + "_ENABLED");
            sb.AppendLine("\tregister_" + mod + "_types();");
            sb.AppendLine("#endif");
        }
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("}" + lf);

        sb.AppendLine("void unregister_module_types() {" + lf);
        foreach (String mod in mods)
        {
            sb.AppendLine("#ifdef MODULE_" + mod.ToUpper() + "_ENABLED");
            sb.AppendLine("\tunregister_" + mod + "_types();");
            sb.AppendLine("#endif");
        }
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("}" + lf);

        addGeneratedFile("modules/register_module_types.gen.cpp", sb.ToString());

        filter("files:modules/register_module_types.gen.cpp");

        List<String> modules = Directory.GetDirectories(Path.Combine(getCsDir(), "modules")).
            Select(x => Path.GetFileName(x)).ToList();
        modules.Remove("mono");
        modules.Remove("ogg");
        modules.Remove("opus");
        modules.Remove("theora");
        modules.Remove("vorbis");
        modules.Remove("webm");
        if (mainPlatform == "android")
        {
            modules.Remove("squish");
            modules.Remove("etc");
            modules.Remove("gridmap");
        }
        String[] moduleDefines = modules.Select( x => "MODULE_" + x.ToUpper() + "_ENABLED").ToArray();

        defines(moduleDefines);
        filter();

        addGeneratedFile(@"core\global_defaults.gen.cpp", 
@"#include ""project_settings.h""
#include ""platform/android/globals/global_defaults.h""
#include ""platform/iphone/globals/global_defaults.h""
void ProjectSettings::register_global_defaults() {
	register_android_global_defaults();
	register_iphone_global_defaults();

}
");

        String gdnative_api_json = Path.Combine(csDir, "modules/gdnative/gdnative_api.json");
        var json = System.IO.File.ReadAllText(gdnative_api_json);
        GDNativeMain api = (GDNativeMain)new JavaScriptSerializer().Deserialize(json, typeof(GDNativeMain));

        //sb.AppendLine("/* THIS FILE IS GENERATED DO NOT EDIT */");
        //sb.AppendLine();
        //sb.AppendLine("#include <gdnative_api_struct.gen.h>");

        //-----------------------------------------------------------------
        // gdnative_api_struct.gen.h generation
        //-----------------------------------------------------------------
        sb.Clear();
        sb.AppendLine(
@"/* THIS FILE IS GENERATED DO NOT EDIT */
#ifndef GODOT_GDNATIVE_API_STRUCT_H
#define GODOT_GDNATIVE_API_STRUCT_H

#include <gdnative/gdnative.h>
#include <nativearvr/godot_nativearvr.h>
#include <nativescript/godot_nativescript.h>
#include <pluginscript/godot_pluginscript.h>

#define GDNATIVE_API_INIT(options) do { extern const godot_gdnative_api_struct *_gdnative_wrapper_api_struct; _gdnative_wrapper_api_struct = options->api_struct; } while (0)

#ifdef __cplusplus
extern ""C"" {
#endif

typedef struct godot_gdnative_api_struct {
	void *next;
	const char *version;");

        foreach (NativeMethod nm in api.api)
        {
            sb.Append("\t" + nm.return_type);
            if (!nm.return_type.EndsWith("*"))
                sb.Append(" ");
            sb.Append("(*" + nm.name + ")(");
            bool bfirst = true;
            foreach (var typeName in nm.arguments)
            {
                if (!bfirst) sb.Append(", ");
                bfirst = false;
                sb.Append(typeName[0]);
                if (!typeName[0].EndsWith("*"))
                    sb.Append(" ");
                sb.Append(typeName[1]);
            }
            sb.AppendLine(");");
        }

        sb.AppendLine(
 @"} godot_gdnative_api_struct;

#ifdef __cplusplus
}
#endif

#endif // GODOT_GDNATIVE_API_STRUCT_H");

        addGeneratedFile("modules/gdnative/include/gdnative_api_struct.gen.h", sb.ToString());

        //-----------------------------------------------------------------
        // gdnative_api_struct.gen.cpp generation
        //-----------------------------------------------------------------
        sb.Clear();
        sb.AppendLine(
@"/* THIS FILE IS GENERATED DO NOT EDIT */

#include <gdnative_api_struct.gen.h>

const char *_gdnative_api_version = ""1.0.0"";
extern const godot_gdnative_api_struct api_struct = {
	NULL,
	_gdnative_api_version,");

        foreach (NativeMethod nm in api.api)
            sb.AppendLine("\t" + nm.name + ",");

        sb.AppendLine( "};");
        addGeneratedFile("modules/gdnative/gdnative_api_struct.gen.cpp", sb.ToString());

        addGeneratedFile("core/script_encryption_key.gen.cpp",
@"#include ""project_settings.h""
uint8_t script_encryption_key[32]={0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0, 0,0,0,0,0,0,0,0};");

        if (mainPlatform == "windows")
        {
            links("wsock32.lib", "Ws2_32.lib", "opengl32.lib",
                // DirectSoundCreate 
                "winmm.lib",
                // DirectSoundCreate
                "dsound.lib",
                // DirectInput8Create
                "dinput8.lib",
                // GUID_XAxis
                "dxguid.lib",
                // GetAdaptersAddresses
                "iphlpapi.lib",
                // PathFileExists
                "Shlwapi.lib"
            );

            CCpp_CodeGeneration_EnableCppExceptions(EExceptionHandling.NoExceptionHandling);
        }
        else
        {
            files(@"drivers\unix\os_unix_global_settings_path.gen.cpp");
            links("OpenSLES;EGL;GLESv3;android;log;z;dl;m");
            buildoptions("-ffunction-sections");
            linkoptions("-Wl,--gc-sections");
            // -Wl,--incremental
        }

        filter("Debug");
            symbols("on");
            optimize("off");

        filter("ReleaseDebug");
            symbols("on");
            optimize("off");
            //Linker_Optimizations_References(true);
            //Linker_Advanced_Profile(true);

        filter("Release");
            optimize("speed");
            symbols("on");
            Linker_General_EnableIncrementalLinking(false);
            //Linker_Optimizations_References(true);
            //Linker_Advanced_Profile(true);

        filter("files:core/*.cpp", "(Release|ReleaseDebug)");
            optimize("speed");
        filter();

        //filter("files:**core_bind.cpp");
        //    symbols("off");
        //    buildoptions("/GS- /Gy- /Oy /Oi-");

        //String file = File.ReadAllText(@"C:\Prototyping\user_interface_analysis\godot.git\log_release_debug_2.txt");
        //String[] cppFiles = Regex.Matches(file, "^cl.*/c ([^ ]*)", RegexOptions.Multiline).Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
        //String[] cppFiles2 = m_project.files.Select(x => x.relativePath).ToArray();

        //foreach (String f in cppFiles2)
        //{
        //    if (!cppFiles.Contains(f) && !f.EndsWith(".h") && !f.EndsWith(".cs"))
        //    {
        //        Console.WriteLine("File " + f + " does not exists in original project");
        //    }
        //}


    } //Main
}; //class Builder

public class NativeMethod
{
    public String name;
    public String return_type;
    public String[][] arguments;
}

public class GDNativeMain
{
    public String version;
    public NativeMethod[] api;
}

