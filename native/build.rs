use std::path::PathBuf;

fn main() {
    let out_dir = PathBuf::from(std::env::var("OUT_DIR").unwrap());
    let out_file = out_dir.join("NativeMethods.g.cs");

    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("typst_native")
        .csharp_namespace("NTypst.Native")
        .generate_csharp_file(out_file)
        .expect("failed to generate bindings");
}
