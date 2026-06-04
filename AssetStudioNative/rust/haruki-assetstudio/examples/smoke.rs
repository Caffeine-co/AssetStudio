use haruki_assetstudio::{
    AssetStudioLibrary, ObjectLookupRequestOptions, ObjectReadByIndexRequest, ObjectReadByPathIdRequest,
};

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let mut args = std::env::args().skip(1);
    let library_path = args.next().expect("native library path");
    let input_path = args.next().expect("Unity asset or folder path");

    let library = AssetStudioLibrary::load(library_path)?;
    let capabilities = library.capabilities()?;
    println!(
        "native streaming source kinds: {}",
        capabilities.native_streaming_payload_kinds.join(",")
    );
    let context = library.open(&input_path, None, &[], false)?;
    let objects = context.list_objects(0, 32, &[])?;
    println!(
        "opened context {} with {} object(s) on first page",
        context.id(),
        objects.len()
    );

    if let Some(first) = objects.first() {
        let lookup = context.lookup_objects(ObjectLookupRequestOptions::path_id(first.path_id))?;
        println!("lookup by path_id returned {} object(s)", lookup.len());
        let read_by_path = context.read_by_path_id_retry(&[ObjectReadByPathIdRequest {
            path_id: first.path_id,
            kind: "raw",
            image_format: "bmp",
        }])?;
        println!("path-id read {} byte(s)", read_by_path.payload.len());
        let read = context.read_by_index_retry(&[ObjectReadByIndexRequest {
            object_index: first.index,
            kind: "raw",
            image_format: "bmp",
        }])?;
        let first_item = read.items.first();
        println!(
            "read {} byte(s), {} item(s), first kind={}",
            read.payload.len(),
            read.returned_count,
            first_item.map_or("", |item| item.payload_kind.as_str())
        );
    }

    Ok(())
}
