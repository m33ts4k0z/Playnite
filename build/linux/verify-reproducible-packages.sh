#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
    echo "usage: verify-reproducible-packages.sh FIRST_OUTPUT_DIR SECOND_OUTPUT_DIR" >&2
    exit 2
fi

first_dir=$(realpath "$1")
second_dir=$(realpath "$2")

for package_dir in "$first_dir" "$second_dir"; do
    test -f "$package_dir/SHA256SUMS" || {
        echo "missing package checksum manifest: $package_dir/SHA256SUMS" >&2
        exit 1
    }
    (cd "$package_dir" && sha256sum --check --strict SHA256SUMS)
done

mapfile -t first_files < <(
    find "$first_dir" -maxdepth 1 -type f \
        \( -name '*.tar.gz' -o -name '*.AppImage' \) -printf '%f\n' | sort
)
mapfile -t second_files < <(
    find "$second_dir" -maxdepth 1 -type f \
        \( -name '*.tar.gz' -o -name '*.AppImage' \) -printf '%f\n' | sort
)

if [[ ${#first_files[@]} -lt 2 ]]; then
    echo "The reference build did not contain both tar and AppDir archives." >&2
    exit 1
fi
if [[ "${first_files[*]}" != "${second_files[*]}" ]]; then
    echo "Repeat build produced a different package set." >&2
    printf 'first:  %s\nsecond: %s\n' "${first_files[*]}" "${second_files[*]}" >&2
    exit 1
fi

for package_name in "${first_files[@]}"; do
    if ! cmp --silent "$first_dir/$package_name" "$second_dir/$package_name"; then
        echo "Package is not reproducible: $package_name" >&2
        sha256sum "$first_dir/$package_name" "$second_dir/$package_name" >&2
        exit 1
    fi
done

if ! cmp --silent "$first_dir/SHA256SUMS" "$second_dir/SHA256SUMS"; then
    echo "Repeat build produced a different SHA256SUMS manifest." >&2
    exit 1
fi

echo "SOURCE_DATE_EPOCH reproducibility passed for ${#first_files[@]} Linux package artifacts."
