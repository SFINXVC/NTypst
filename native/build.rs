fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("typst_native")
        .csharp_namespace("NTypst.Native")
        .generate_csharp_file("../src/NTypst.Native/Generated/NativeMethods.g.cs")
        .unwrap()
}
