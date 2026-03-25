use chrono::{DateTime, Datelike, Timelike, Utc};
use std::ffi::{CStr, CString, c_char};
use typst::{
    Library, LibraryExt,
    diag::{FileError, FileResult, Severity, SourceDiagnostic},
    foundations::{Bytes, Datetime},
    syntax::{FileId, Source, VirtualPath},
    text::{Font, FontBook},
    utils::LazyHash,
};

pub type ResolveCallback = extern "C" fn(*const c_char, *mut *mut u8, *mut usize) -> i32;
pub type FreeCallback = extern "C" fn(*mut u8, usize);

pub enum FontEnum {
    Font(Font),
    Slot(typst_kit::fonts::FontSlot),
}

struct TypstWorld {
    library: LazyHash<Library>,
    main_source_id: FileId,
    now: DateTime<Utc>,
    book: LazyHash<FontBook>,
    fonts: Vec<FontEnum>,
    resolve_source_cb: ResolveCallback,
    resolve_file_cb: ResolveCallback,
    free_cb: FreeCallback,
}

impl TypstWorld {
    fn resolve_bytes(&self, id: FileId, resolve_cb: ResolveCallback) -> FileResult<Bytes> {
        let path_str = if let Some(package) = id.package() {
            format!(
                "@{}/{}/{}{}",
                package.namespace,
                package.name,
                package.version,
                id.vpath().as_rooted_path().to_str().unwrap_or("/")
            )
        } else {
            id.vpath()
                .as_rooted_path()
                .to_str()
                .ok_or_else(|| FileError::Other(Some("path is not valid UTF-8".into())))?
                .to_string()
        };

        let c_path = CString::new(path_str.as_str())
            .map_err(|_| FileError::Other(Some("path contains interior NUL byte".into())))?;

        let mut ptr = std::ptr::null_mut::<u8>();
        let mut len = 0usize;

        let code = resolve_cb(c_path.as_ptr(), &mut ptr, &mut len);

        match code {
            0 => {
                if len > 0 && ptr.is_null() {
                    return Err(FileError::Other(Some(
                        "resolver returned a null buffer for non-empty content".into(),
                    )));
                }

                let bytes = unsafe {
                    let slice = std::slice::from_raw_parts(ptr as *const u8, len);
                    let copied = Bytes::new(slice.to_vec());
                    (self.free_cb)(ptr, len);
                    copied
                };

                Ok(bytes)
            }
            -1 => Err(FileError::NotFound(id.vpath().as_rooted_path().into())),
            -2 => Err(FileError::Other(Some(
                format!("failed to resolve {}", path_str).into(),
            ))),
            _ => Err(FileError::Other(Some(
                format!("resolver returned unknown status code {code}").into(),
            ))),
        }
    }
}

impl typst::World for TypstWorld {
    fn library(&self) -> &LazyHash<Library> {
        &self.library
    }

    fn book(&self) -> &LazyHash<FontBook> {
        &self.book
    }

    fn main(&self) -> FileId {
        self.main_source_id
    }

    fn source(&self, id: FileId) -> FileResult<Source> {
        let bytes = self.resolve_bytes(id, self.resolve_source_cb)?;
        let text = std::str::from_utf8(&bytes).map_err(|err| {
            FileError::Other(Some(format!("source is not valid UTF-8: {err}").into()))
        })?;
        Ok(Source::new(id, text.into()))
    }

    fn file(&self, id: FileId) -> FileResult<Bytes> {
        self.resolve_bytes(id, self.resolve_file_cb)
    }

    fn font(&self, index: usize) -> Option<Font> {
        self.fonts.get(index).and_then(|font_enum| match font_enum {
            FontEnum::Font(font) => Some(font.clone()),
            FontEnum::Slot(slot) => slot.get(),
        })
    }

    fn today(&self, offset: Option<i64>) -> Option<Datetime> {
        let now = match offset {
            Some(hours) => self.now + chrono::TimeDelta::hours(hours),
            None => self.now,
        };

        Datetime::from_ymd_hms(
            now.year(),
            now.month().try_into().ok()?,
            now.day().try_into().ok()?,
            now.hour().try_into().ok()?,
            now.minute().try_into().ok()?,
            now.second().try_into().ok()?,
        )
    }
}

pub struct TypstWorldHandle(TypstWorld);
pub struct TypstPagedDocument(typst::layout::PagedDocument);
pub struct TypstHtmlDocument(typst_html::HtmlDocument);
pub struct TypstBuffer(Vec<u8>);

struct PreparedDiagnostic {
    severity: i32,
    message: CString,
    hints: Vec<CString>,
}

struct CompileResultInner {
    document: Option<Box<dyn std::any::Any>>,
    warnings: Vec<PreparedDiagnostic>,
    errors: Vec<PreparedDiagnostic>,
}

pub struct TypstCompileResult(CompileResultInner);

fn to_cstring(s: &str) -> CString {
    CString::new(s.replace('\0', "\u{FFFD}")).unwrap_or_default()
}

fn prepare_diagnostic(d: &SourceDiagnostic) -> PreparedDiagnostic {
    PreparedDiagnostic {
        severity: match d.severity {
            Severity::Warning => 0,
            Severity::Error => 1,
        },
        message: to_cstring(&d.message),
        hints: d.hints.iter().map(|h| to_cstring(h)).collect(),
    }
}

fn compile_inner<D: typst::Document + 'static>(world: &TypstWorld) -> *mut TypstCompileResult {
    let warned = typst::compile::<D>(world);

    let warnings: Vec<PreparedDiagnostic> =
        warned.warnings.iter().map(prepare_diagnostic).collect();

    let (document, errors): (Option<Box<dyn std::any::Any>>, Vec<PreparedDiagnostic>) =
        match warned.output {
            Ok(doc) => (Some(Box::new(doc)), Vec::new()),
            Err(errs) => (None, errs.iter().map(prepare_diagnostic).collect()),
        };

    Box::into_raw(Box::new(TypstCompileResult(CompileResultInner {
        document,
        warnings,
        errors,
    })))
}

fn get_diagnostic(
    result: &TypstCompileResult,
    kind: i32,
    index: usize,
) -> Option<&PreparedDiagnostic> {
    let list = if kind == 0 {
        &result.0.warnings
    } else {
        &result.0.errors
    };
    list.get(index)
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_world_new(
    resolve_source_cb: ResolveCallback,
    resolve_file_cb: ResolveCallback,
    free_cb: FreeCallback,
) -> *mut TypstWorldHandle {
    let world = TypstWorld {
        library: LazyHash::new(Library::builder().build()),
        main_source_id: FileId::new(None, VirtualPath::new("/")),
        now: Utc::now(),
        book: LazyHash::new(FontBook::new()),
        fonts: Vec::new(),
        resolve_source_cb,
        resolve_file_cb,
        free_cb,
    };
    Box::into_raw(Box::new(TypstWorldHandle(world)))
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_world_free(world: *mut TypstWorldHandle) {
    if !world.is_null() {
        unsafe {
            drop(Box::from_raw(world));
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_world_set_main(world: *mut TypstWorldHandle, path: *const c_char) {
    let world = unsafe { &mut (*world).0 };
    let path = unsafe { CStr::from_ptr(path) }.to_str().unwrap_or("/");
    world.main_source_id = FileId::new(None, VirtualPath::new(path));
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_world_add_font(
    world: *mut TypstWorldHandle,
    data: *const u8,
    len: usize,
) -> i32 {
    let world = unsafe { &mut (*world).0 };
    let data = unsafe { std::slice::from_raw_parts(data, len) };
    let bytes = Bytes::new(data.to_vec());
    let mut count = 0i32;
    for font in Font::iter(bytes) {
        world.book.push(font.info().clone());
        world.fonts.push(FontEnum::Font(font));
        count += 1;
    }
    count
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_world_add_system_fonts(world: *mut TypstWorldHandle) -> i32 {
    let world = unsafe { &mut (*world).0 };
    let system = typst_kit::fonts::Fonts::searcher()
        .include_system_fonts(true)
        .search();

    let count = system.fonts.len();

    for i in 0..count {
        if let Some(info) = system.book.info(i) {
            world.book.push(info.clone());
        }
    }

    for slot in system.fonts {
        world.fonts.push(FontEnum::Slot(slot));
    }

    count as i32
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_paged(world: *const TypstWorldHandle) -> *mut TypstCompileResult {
    let world = unsafe { &(*world).0 };
    compile_inner::<typst::layout::PagedDocument>(world)
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_html(world: *const TypstWorldHandle) -> *mut TypstCompileResult {
    let world = unsafe { &(*world).0 };
    compile_inner::<typst_html::HtmlDocument>(world)
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_is_success(result: *const TypstCompileResult) -> bool {
    unsafe { (*result).0.document.is_some() }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_take_paged_document(
    result: *mut TypstCompileResult,
) -> *mut TypstPagedDocument {
    let inner = unsafe { &mut (*result).0 };
    inner
        .document
        .take()
        .and_then(|d| d.downcast::<typst::layout::PagedDocument>().ok())
        .map(|d| Box::into_raw(Box::new(TypstPagedDocument(*d))))
        .unwrap_or(std::ptr::null_mut())
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_take_html_document(
    result: *mut TypstCompileResult,
) -> *mut TypstHtmlDocument {
    let inner = unsafe { &mut (*result).0 };
    inner
        .document
        .take()
        .and_then(|d| d.downcast::<typst_html::HtmlDocument>().ok())
        .map(|d| Box::into_raw(Box::new(TypstHtmlDocument(*d))))
        .unwrap_or(std::ptr::null_mut())
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_warning_count(result: *const TypstCompileResult) -> usize {
    unsafe { (*result).0.warnings.len() }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_error_count(result: *const TypstCompileResult) -> usize {
    unsafe { (*result).0.errors.len() }
}

/// Returns a borrowed pointer to the diagnostic message. Valid until the result is freed.
/// `kind`: 0 = warning, 1 = error.
#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_diagnostic_message(
    result: *const TypstCompileResult,
    kind: i32,
    index: usize,
) -> *const c_char {
    let result = unsafe { &*result };
    get_diagnostic(result, kind, index)
        .map(|d| d.message.as_ptr())
        .unwrap_or(std::ptr::null())
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_diagnostic_severity(
    result: *const TypstCompileResult,
    kind: i32,
    index: usize,
) -> i32 {
    let result = unsafe { &*result };
    get_diagnostic(result, kind, index)
        .map(|d| d.severity)
        .unwrap_or(-1)
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_diagnostic_hint_count(
    result: *const TypstCompileResult,
    kind: i32,
    index: usize,
) -> usize {
    let result = unsafe { &*result };
    get_diagnostic(result, kind, index)
        .map(|d| d.hints.len())
        .unwrap_or(0)
}

/// Returns a borrowed pointer to a diagnostic hint string. Valid until the result is freed.
#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_diagnostic_hint(
    result: *const TypstCompileResult,
    kind: i32,
    index: usize,
    hint_index: usize,
) -> *const c_char {
    let result = unsafe { &*result };
    get_diagnostic(result, kind, index)
        .and_then(|d| d.hints.get(hint_index))
        .map(|h| h.as_ptr())
        .unwrap_or(std::ptr::null())
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_compile_result_free(result: *mut TypstCompileResult) {
    if !result.is_null() {
        unsafe {
            drop(Box::from_raw(result));
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_paged_document_page_count(doc: *const TypstPagedDocument) -> usize {
    unsafe { (*doc).0.pages.len() }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_paged_document_free(doc: *mut TypstPagedDocument) {
    if !doc.is_null() {
        unsafe {
            drop(Box::from_raw(doc));
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_paged_document_export_pdf(
    doc: *const TypstPagedDocument,
) -> *mut TypstBuffer {
    let doc = unsafe { &(*doc).0 };
    let options = typst_pdf::PdfOptions::default();
    match typst_pdf::pdf(doc, &options) {
        Ok(bytes) => Box::into_raw(Box::new(TypstBuffer(bytes))),
        Err(_) => std::ptr::null_mut(),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_paged_document_export_png(
    doc: *const TypstPagedDocument,
    page_index: usize,
    pixel_per_pt: f32,
) -> *mut TypstBuffer {
    let doc = unsafe { &(*doc).0 };
    if page_index >= doc.pages.len() {
        return std::ptr::null_mut();
    }
    let pixmap = typst_render::render(&doc.pages[page_index], pixel_per_pt);
    match pixmap.encode_png() {
        Ok(bytes) => Box::into_raw(Box::new(TypstBuffer(bytes))),
        Err(_) => std::ptr::null_mut(),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_paged_document_export_svg(
    doc: *const TypstPagedDocument,
) -> *mut TypstBuffer {
    let doc = unsafe { &(*doc).0 };
    let svg = typst_svg::svg_merged(doc, typst::layout::Abs::zero());
    Box::into_raw(Box::new(TypstBuffer(svg.into_bytes())))
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_paged_document_export_svg_page(
    doc: *const TypstPagedDocument,
    page_index: usize,
) -> *mut TypstBuffer {
    let doc = unsafe { &(*doc).0 };
    if page_index >= doc.pages.len() {
        return std::ptr::null_mut();
    }
    let svg = typst_svg::svg(&doc.pages[page_index]);
    Box::into_raw(Box::new(TypstBuffer(svg.into_bytes())))
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_html_document_export(doc: *const TypstHtmlDocument) -> *mut TypstBuffer {
    let doc = unsafe { &(*doc).0 };
    match typst_html::html(doc) {
        Ok(html) => Box::into_raw(Box::new(TypstBuffer(html.into_bytes()))),
        Err(_) => std::ptr::null_mut(),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_html_document_free(doc: *mut TypstHtmlDocument) {
    if !doc.is_null() {
        unsafe {
            drop(Box::from_raw(doc));
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_buffer_data(buf: *const TypstBuffer) -> *const u8 {
    unsafe { (*buf).0.as_ptr() }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_buffer_len(buf: *const TypstBuffer) -> usize {
    unsafe { (*buf).0.len() }
}

#[unsafe(no_mangle)]
pub extern "C" fn typst_buffer_free(buf: *mut TypstBuffer) {
    if !buf.is_null() {
        unsafe {
            drop(Box::from_raw(buf));
        }
    }
}
